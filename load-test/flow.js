/**
 * The virtual player shared by smoke.js, scenario.js and stress.js.
 *
 * Each k6 VU owns one guest player and behaves like the Unity client:
 *
 *   launch:    POST /auth/guest -> GET /config -> POST /economy/daily-reward -> GET /events
 *   iteration: GET /players/me -> POST /levels/{n}/start -> think -> greedySolve
 *              -> POST /levels/{n}/complete (or /fail) -> GET /economy/wallet -> GET /leaderboards/weekly
 *
 * The server replays every move list, so the level is solved locally with the engine port and
 * the VU waits at least the human minimum (150 ms per TAP) before reporting. A player has five
 * lives and regains one every 30 minutes, so once the server answers NO_LIVES_LEFT the VU simply
 * becomes a brand new guest. All state lives in module variables: k6 gives every VU its own
 * JavaScript runtime, so this module is instantiated once per VU and persists across iterations.
 */
import http from 'k6/http';
import { sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import exec from 'k6/execution';

import { greedySolve } from './engine.js';
import {
  API,
  BASE_URL,
  IDEMPOTENCY_KEY_HEADER,
  checkJson,
  envBool,
  envInt,
  expect,
  requestParams,
  secondsUntilNextMinute,
  uuidv4,
} from './config.js';

// ------------------------------------------------------------------ custom metrics (init context)

/** Latency of every step, one Trend per endpoint, so the summary can show p95 per step. */
export const steps = {
  login: new Trend('step_login', true),
  config: new Trend('step_config', true),
  dailyReward: new Trend('step_daily_reward', true),
  events: new Trend('step_events', true),
  profile: new Trend('step_profile', true),
  levelStart: new Trend('step_level_start', true),
  levelComplete: new Trend('step_level_complete', true),
  levelFail: new Trend('step_level_fail', true),
  wallet: new Trend('step_wallet', true),
  leaderboard: new Trend('step_leaderboard', true),
};

/** Business outcomes: what happened to the players, independent of HTTP status codes. */
export const counters = {
  levelsCompleted: new Counter('levels_completed'),
  levelsFailed: new Counter('levels_failed'),
  completionsRejected: new Counter('completions_rejected'),
  completionsReplayed: new Counter('completions_replayed'),
  businessRejections: new Counter('business_rejections'),
  rateLimited: new Counter('rate_limited'),
  noLivesLeft: new Counter('no_lives_left'),
  playersCreated: new Counter('players_created'),
  retries: new Counter('retries'),
};

/** Share of steps whose outcome was neither success nor an expected business rejection. */
export const errors = new Rate('errors');

/** Milliseconds the greedy solver needed per level (client CPU, not server time). */
export const solverTime = new Trend('solver_time', true);

/** TAP moves played per level. */
export const levelMoves = new Trend('level_moves');

/** Simulated play time per level (what the VU sleeps between start and complete). */
export const thinkTime = new Trend('think_time', true);

/** Error codes the server is expected to answer with during normal play (never counted as errors). */
const EXPECTED_BUSINESS_CODES = ['NO_LIVES_LEFT', 'LEVEL_LOCKED', 'DAILY_REWARD_ALREADY_CLAIMED', 'SUSPICIOUS_DURATION'];

/** Server minimum per TAP move (blastscale.gameplay.min-millis-per-move); faster is rejected. */
export const SERVER_MIN_MS_PER_MOVE = 150;

/** How many unexpected failures each VU logs before going quiet (keeps 2000 VUs from flooding stdout). */
const MAX_LOGGED_FAILURES_PER_VU = 5;

// ------------------------------------------------------------------ settings

/**
 * Resolves the tunables of the flow from environment variables on top of the script's defaults.
 * All durations are milliseconds.
 */
export function buildSettings(overrides) {
  const base = Object.assign(
    {
      thinkMsPerMove: 200, // simulated thinking per TAP; must stay >= 150 or completions are rejected
      thinkBaseMs: 300, // fixed part of the play time (reading the board, animations)
      lobbyPauseMs: 1000, // pause on the level map between two levels (randomised 50-150%)
      minIterationMs: 1000, // pacing floor that keeps a player far below 600 requests/minute
      maxLevel: 0, // 0 = play the player's real current level; N = never go past level N
      perVuClientIp: false, // X-Forwarded-For per VU only matters when bypassing nginx (see README, "rate limits")
      leaderboardLimit: 10,
      verbose: false, // log every step (smoke test)
    },
    overrides || {},
  );
  return {
    thinkMsPerMove: envInt('THINK_MS_PER_MOVE', base.thinkMsPerMove),
    thinkBaseMs: envInt('THINK_BASE_MS', base.thinkBaseMs),
    lobbyPauseMs: envInt('LOBBY_PAUSE_MS', base.lobbyPauseMs),
    minIterationMs: envInt('MIN_ITERATION_MS', base.minIterationMs),
    maxLevel: envInt('MAX_LEVEL', base.maxLevel),
    perVuClientIp: envBool('PER_VU_CLIENT_IP', base.perVuClientIp),
    leaderboardLimit: envInt('LEADERBOARD_LIMIT', base.leaderboardLimit),
    verbose: envBool('VERBOSE', base.verbose),
  };
}

/**
 * Shared setup(): checks that the API answers, logs the effective settings once and returns the
 * data every VU receives (a run id that keeps device ids unique across test runs).
 */
export function prepareRun(scriptName, settings) {
  const runId = new Date().toISOString().replace(/[-:]/g, '').replace(/\.\d+Z$/, 'Z').toLowerCase();
  console.log(`[${scriptName}] target ${BASE_URL}, run id ${runId}`);
  console.log(`[${scriptName}] settings ${JSON.stringify(settings)}`);
  if (settings.thinkMsPerMove < SERVER_MIN_MS_PER_MOVE) {
    console.warn(
      `[${scriptName}] THINK_MS_PER_MOVE=${settings.thinkMsPerMove} is below the server minimum of ` +
        `${SERVER_MIN_MS_PER_MOVE} ms: completions will be rejected with SUSPICIOUS_DURATION`,
    );
  }
  const health = http.get(`${BASE_URL}/actuator/health/readiness`, {
    tags: { name: 'GET /actuator/health/readiness' },
    timeout: '10s',
  });
  if (health.status === 0 || health.status >= 500) {
    exec.test.abort(`API not reachable at ${BASE_URL} (readiness probe returned status ${health.status}: ${health.error || health.body})`);
  } else if (health.status !== 200) {
    console.warn(`[${scriptName}] readiness probe returned ${health.status}; continuing anyway`);
  }
  return { runId };
}

/** Builds the per-iteration context: run id, settings and the headers this VU adds to every call. */
export function buildContext(data, settings) {
  const extraHeaders = settings.perVuClientIp ? { 'X-Forwarded-For': clientIp(__VU) } : {};
  return { runId: data && data.runId ? data.runId : 'local', settings, extraHeaders };
}

// ------------------------------------------------------------------ per-VU state

/** The guest player currently owned by this VU (null before the first launch). */
let player = null;

/** Incremented every time the VU replaces its player, keeping device ids unique. */
let generation = 0;

/** Unexpected failures logged so far by this VU. */
let loggedFailures = 0;

/** Exposes the current player (used by the smoke test to print who played). */
export function currentPlayer() {
  return player;
}

/** Deterministic private IP per VU so the anonymous per-IP rate limit sees many devices. */
function clientIp(vu) {
  return `10.${(vu >> 16) & 255}.${(vu >> 8) & 255}.${vu & 255}`;
}

/** Records the outcome of one step in the `errors` rate. */
function outcome(ok) {
  errors.add(ok ? 0 : 1);
}

/** Logs the first few unexpected failures of this VU with enough context to debug them. */
function logFailure(step, out, res) {
  if (loggedFailures >= MAX_LOGGED_FAILURES_PER_VU) {
    return;
  }
  loggedFailures++;
  const body = res && res.body ? String(res.body).slice(0, 300) : '';
  console.error(`[vu ${__VU}] ${step} failed: status ${out.status} code ${out.code || '-'} ${res && res.error ? res.error : ''} ${body}`);
}

/**
 * Handles a response that was neither a success nor an expected business rejection.
 * 429s wait for the next rate-limit window; other failures back off briefly so a dead API
 * does not turn every VU into a tight error loop.
 */
function handleFailure(step, out, res, ctx) {
  if (out.status === 429) {
    counters.rateLimited.add(1, { step });
    outcome(false);
    if (ctx.settings.verbose || loggedFailures < MAX_LOGGED_FAILURES_PER_VU) {
      loggedFailures++;
      console.warn(`[vu ${__VU}] ${step}: rate limited (429), waiting for the next window`);
    }
    sleep(secondsUntilNextMinute());
    return;
  }
  outcome(false);
  logFailure(step, out, res);
  sleep(1 + Math.random());
}

/** Whether a response deserves one retry with the same idempotency key (lost or proxy-failed request). */
function isTransient(res) {
  return res.status === 0 || res.status === 502 || res.status === 503 || res.status === 504;
}

// ------------------------------------------------------------------ steps

/** POST /auth/guest for a fresh device id; the server creates the player on first use. */
function login(ctx) {
  const deviceId = `k6-${ctx.runId}-vu${__VU}-g${generation}`;
  const res = http.post(
    `${API}/auth/guest`,
    JSON.stringify({ deviceId }),
    requestParams('POST /api/v1/auth/guest', null, expect.ok, ctx.extraHeaders),
  );
  steps.login.add(res.timings.duration);
  const out = checkJson(res, 'guest login', [200]);
  if (out.ok && out.body.token) {
    player = {
      deviceId,
      token: out.body.token,
      playerId: out.body.playerId,
      username: out.body.username,
      level: 1,
      levelsPlayed: 0,
    };
    counters.playersCreated.add(1);
    outcome(true);
    if (ctx.settings.verbose) {
      console.log(`[vu ${__VU}] logged in as ${player.username} (player ${player.playerId}, device ${deviceId})`);
    }
    return true;
  }
  handleFailure('guest login', out, res, ctx);
  return false;
}

/** GET /config: remote configuration and experiment assignments, fetched once per launch. */
function fetchConfig(ctx) {
  const res = http.get(`${API}/config`, requestParams('GET /api/v1/config', player.token, expect.ok, ctx.extraHeaders));
  steps.config.add(res.timings.duration);
  const out = checkJson(res, 'config', [200]);
  if (out.ok && out.body.config) {
    outcome(true);
    if (ctx.settings.verbose) {
      console.log(`[vu ${__VU}] config: maxLives=${out.body.config.maxLives} experiments=${(out.body.experiments || []).length}`);
    }
    return;
  }
  handleFailure('config', out, res, ctx);
}

/** POST /economy/daily-reward once per launch; a repeat claim answers 409 DAILY_REWARD_ALREADY_CLAIMED. */
function claimDailyReward(ctx) {
  const headers = Object.assign({ [IDEMPOTENCY_KEY_HEADER]: uuidv4() }, ctx.extraHeaders);
  const res = http.post(
    `${API}/economy/daily-reward`,
    null,
    requestParams('POST /api/v1/economy/daily-reward', player.token, expect.okOrConflict, headers),
  );
  steps.dailyReward.add(res.timings.duration);
  const out = checkJson(res, 'daily reward', [200, 409]);
  if (out.status === 200 && out.body) {
    outcome(true);
    if (ctx.settings.verbose) {
      console.log(`[vu ${__VU}] daily reward: +${out.body.coins} coins (streak ${out.body.streak})`);
    }
    return;
  }
  if (out.status === 409 && out.code === 'DAILY_REWARD_ALREADY_CLAIMED') {
    counters.businessRejections.add(1, { code: out.code });
    outcome(true);
    return;
  }
  handleFailure('daily reward', out, res, ctx);
}

/** GET /events: active live events with the player's standing. */
function fetchEvents(ctx) {
  const res = http.get(`${API}/events`, requestParams('GET /api/v1/events', player.token, expect.ok, ctx.extraHeaders));
  steps.events.add(res.timings.duration);
  const out = checkJson(res, 'events', [200]);
  if (out.ok && Array.isArray(out.body)) {
    outcome(true);
    if (ctx.settings.verbose) {
      console.log(`[vu ${__VU}] events: ${out.body.length} active`);
    }
    return;
  }
  handleFailure('events', out, res, ctx);
}

/** GET /players/me: refreshes the player's current level from the server. */
function fetchProfile(ctx) {
  const res = http.get(`${API}/players/me`, requestParams('GET /api/v1/players/me', player.token, expect.ok, ctx.extraHeaders));
  steps.profile.add(res.timings.duration);
  const out = checkJson(res, 'profile', [200]);
  if (out.ok && typeof out.body.currentLevel === 'number') {
    player.level = out.body.currentLevel;
    outcome(true);
    return;
  }
  handleFailure('profile', out, res, ctx);
}

/** The level this player plays next: the real current level, optionally capped by MAX_LEVEL. */
function targetLevel(ctx) {
  const level = Math.max(1, player.level || 1);
  return ctx.settings.maxLevel > 0 ? Math.min(level, ctx.settings.maxLevel) : level;
}

/**
 * POST /levels/{level}/start. Returns {session} on success, {noLives: true} when the player is
 * out of lives, {locked: true} when the level is locked, or {} on any other failure.
 */
function startLevel(ctx, level) {
  const res = http.post(
    `${API}/levels/${level}/start`,
    null,
    requestParams('POST /api/v1/levels/{level}/start', player.token, expect.levelStart, ctx.extraHeaders),
  );
  steps.levelStart.add(res.timings.duration);
  const out = checkJson(res, 'level start', [200, 403, 409]);
  if (out.status === 200 && out.body && out.body.sessionId && out.body.board) {
    outcome(true);
    return { session: out.body };
  }
  if (out.status === 409 && out.code === 'NO_LIVES_LEFT') {
    counters.businessRejections.add(1, { code: out.code });
    outcome(true);
    if (ctx.settings.verbose) {
      const next = out.body && out.body.details ? out.body.details.nextLifeInSeconds : '?';
      console.log(`[vu ${__VU}] ${player.username} has no lives left (next in ${next}s); switching to a new guest`);
    }
    return { noLives: true };
  }
  if (out.status === 403 && out.code === 'LEVEL_LOCKED') {
    counters.businessRejections.add(1, { code: out.code });
    outcome(true);
    return { locked: true };
  }
  handleFailure('level start', out, res, ctx);
  return {};
}

/** POST /levels/{level}/complete with the solver's moves; retried once with the same key on transport errors. */
function completeLevel(ctx, level, session, solved) {
  const body = JSON.stringify({
    sessionId: session.sessionId,
    score: solved.score,
    movesUsed: solved.movesUsed,
    moves: solved.moves,
    extraMovesUsed: false,
  });
  const headers = Object.assign({ [IDEMPOTENCY_KEY_HEADER]: uuidv4() }, ctx.extraHeaders);
  const params = requestParams('POST /api/v1/levels/{level}/complete', player.token, expect.levelComplete, headers);
  let res = http.post(`${API}/levels/${level}/complete`, body, params);
  if (isTransient(res)) {
    counters.retries.add(1, { step: 'level complete' });
    sleep(0.5);
    res = http.post(`${API}/levels/${level}/complete`, body, params);
  }
  steps.levelComplete.add(res.timings.duration);
  const out = checkJson(res, 'level complete', [200, 422]);
  if (out.status === 200 && out.body) {
    if (out.body.status === 'COMPLETED') {
      counters.levelsCompleted.add(1);
    } else {
      counters.completionsReplayed.add(1);
    }
    if (typeof out.body.nextLevel === 'number') {
      player.level = out.body.nextLevel;
    }
    player.levelsPlayed++;
    outcome(true);
    if (ctx.settings.verbose) {
      const reward = out.body.reward ? out.body.reward.coins : 0;
      const coins = out.body.wallet ? out.body.wallet.coins : '?';
      console.log(
        `[vu ${__VU}] level ${level}: ${out.body.status}, score ${out.body.score} (${out.body.stars} stars), ` +
          `+${reward} coins -> wallet ${coins}, next level ${out.body.nextLevel}`,
      );
    }
    return;
  }
  if (out.status === 422 || out.status === 404 || out.status === 409) {
    const code = out.code || 'UNKNOWN';
    counters.completionsRejected.add(1, { code });
    if (EXPECTED_BUSINESS_CODES.indexOf(code) !== -1) {
      // SUSPICIOUS_DURATION is the anti-cheat rule the think time is tuned against.
      counters.businessRejections.add(1, { code });
      outcome(true);
      if (ctx.settings.verbose) {
        console.log(`[vu ${__VU}] level ${level}: completion rejected with ${code}`);
      }
      return;
    }
    // SCORE_MISMATCH / INVALID_MOVE_SEQUENCE / OBJECTIVE_NOT_REACHED mean the engine port and
    // the server disagree, which is a real defect, so they count as errors.
    outcome(false);
    logFailure('level complete', out, res);
    return;
  }
  handleFailure('level complete', out, res, ctx);
}

/** POST /levels/{level}/fail when the solver could not reach the target (the life is spent either way). */
function failLevel(ctx, level, session, solved) {
  const body = JSON.stringify({ sessionId: session.sessionId, moves: solved.moves, extraMovesUsed: false });
  const res = http.post(
    `${API}/levels/${level}/fail`,
    body,
    requestParams('POST /api/v1/levels/{level}/fail', player.token, expect.ok, ctx.extraHeaders),
  );
  steps.levelFail.add(res.timings.duration);
  const out = checkJson(res, 'level fail', [200]);
  if (out.ok && out.body.status) {
    counters.levelsFailed.add(1);
    player.levelsPlayed++;
    outcome(true);
    if (ctx.settings.verbose) {
      console.log(`[vu ${__VU}] level ${level}: target missed with ${solved.score} points after ${solved.movesUsed} moves -> ${out.body.status}`);
    }
    return;
  }
  handleFailure('level fail', out, res, ctx);
}

/** GET /economy/wallet after the level: coins, lives and boosters. */
function fetchWallet(ctx) {
  const res = http.get(`${API}/economy/wallet`, requestParams('GET /api/v1/economy/wallet', player.token, expect.ok, ctx.extraHeaders));
  steps.wallet.add(res.timings.duration);
  const out = checkJson(res, 'wallet', [200]);
  if (out.ok && typeof out.body.lives === 'number') {
    outcome(true);
    return;
  }
  handleFailure('wallet', out, res, ctx);
}

/** GET /leaderboards/weekly?limit=N: the weekly top list plus the player's own rank. */
function fetchLeaderboard(ctx) {
  const res = http.get(
    `${API}/leaderboards/weekly?limit=${ctx.settings.leaderboardLimit}`,
    requestParams('GET /api/v1/leaderboards/weekly', player.token, expect.ok, ctx.extraHeaders),
  );
  steps.leaderboard.add(res.timings.duration);
  const out = checkJson(res, 'leaderboard', [200]);
  if (out.ok && Array.isArray(out.body.players)) {
    outcome(true);
    if (ctx.settings.verbose) {
      console.log(`[vu ${__VU}] leaderboard ${out.body.season}: rank ${out.body.myRank} with ${out.body.myScore} points, top ${out.body.players.length}`);
    }
    return;
  }
  handleFailure('leaderboard', out, res, ctx);
}

// ------------------------------------------------------------------ composite behaviour

/** "App launch": log in as a (new) guest, then config, daily reward and the event screen. */
function launch(ctx) {
  if (!login(ctx)) {
    return false;
  }
  fetchConfig(ctx);
  claimDailyReward(ctx);
  fetchEvents(ctx);
  return true;
}

/** Drops the current player and launches as a brand new guest (used after NO_LIVES_LEFT). */
function replacePlayer(ctx) {
  counters.noLivesLeft.add(1);
  generation++;
  player = null;
  return launch(ctx);
}

/** Start -> think -> solve -> complete or fail for the player's current level. Returns true when a level was played. */
function playLevel(ctx) {
  let level = targetLevel(ctx);
  let start = startLevel(ctx, level);
  if (start.noLives) {
    if (!replacePlayer(ctx)) {
      return false;
    }
    level = targetLevel(ctx);
    start = startLevel(ctx, level);
  } else if (start.locked) {
    // Our idea of the current level was stale: re-read the profile and retry once.
    fetchProfile(ctx);
    level = targetLevel(ctx);
    start = startLevel(ctx, level);
  }
  if (!start.session) {
    return false;
  }
  const session = start.session;

  const solveStartedAt = Date.now();
  const solved = greedySolve(session.board, session.seed);
  solverTime.add(Date.now() - solveStartedAt);
  levelMoves.add(solved.moves.length);
  if (ctx.settings.verbose) {
    console.log(
      `[vu ${__VU}] level ${level}: seed ${session.seed}, ${session.board.rows}x${session.board.cols} ` +
        `${session.board.colorCount} colours, target ${session.board.targetScore} -> solver ${solved.score} points ` +
        `in ${solved.movesUsed} moves (${solved.objectiveReached ? 'win' : 'miss'}), lives left ${session.livesRemaining}`,
    );
  }

  // A human needs time for every move; the server rejects anything faster than 150 ms per TAP.
  const thinkMs = ctx.settings.thinkBaseMs + solved.moves.length * ctx.settings.thinkMsPerMove;
  thinkTime.add(thinkMs);
  sleep(thinkMs / 1000);

  if (solved.objectiveReached) {
    completeLevel(ctx, level, session, solved);
  } else {
    failLevel(ctx, level, session, solved);
  }
  return true;
}

/**
 * One iteration of the realistic loop. Launches the app when this VU has no player yet, plays
 * one level, refreshes wallet and leaderboard, then pauses on the level map. The pacing floor
 * keeps every player well below the 600 requests/minute limit even with very short think times.
 */
export function gameLoop(ctx) {
  const startedAt = Date.now();
  if (player === null && !launch(ctx)) {
    return; // could not log in (rate limited or API down); handleFailure already backed off
  }
  fetchProfile(ctx);
  playLevel(ctx);
  if (player === null) {
    return; // the player ran out of lives and the replacement login failed; retry next iteration
  }
  fetchWallet(ctx);
  fetchLeaderboard(ctx);

  const lobbyMs = ctx.settings.lobbyPauseMs * (0.5 + Math.random());
  const elapsedMs = Date.now() - startedAt;
  sleep(Math.max(lobbyMs, ctx.settings.minIterationMs - elapsedMs) / 1000);
}
