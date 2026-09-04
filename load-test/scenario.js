/**
 * Realistic load: many players looping through
 *   guest login -> config -> profile -> start level -> think -> solve -> complete (or fail)
 *   -> wallet -> leaderboard
 * with human think time, so the server accepts the completions.
 *
 * Load profile (environment variables):
 *   VUS=50 DURATION=2m          constant number of players (default)
 *   RAMP=1                      ramp 100 -> 500 -> 1000 players instead
 *   STAGES=1m:100,2m:500,...    custom ramp (<duration>:<VUs> pairs); implies RAMP=1
 *   THINK_MS_PER_MOVE=200       simulated thinking per move (server minimum is 150)
 *   P95_MS=500 MAX_ERROR_RATE=0.01   threshold knobs
 *   RESULTS_DIR=/results        also write the JSON summary there
 *
 *   docker compose --profile loadtest run --rm k6 run -e VUS=100 -e DURATION=2m /scripts/scenario.js
 *   k6 run -e BASE_URL=http://localhost:8080 scenario.js
 */
import { SUMMARY_TREND_STATS, envBool, envInt, envString, parseStages } from './config.js';
import { buildContext, buildSettings, gameLoop, prepareRun } from './flow.js';
import { buildSummary } from './summary.js';

const SCENARIO = 'scenario';

/** Human-like think time (200 ms per move + 300 ms), one second on the level map between levels. */
const settings = buildSettings({ thinkMsPerMove: 200, thinkBaseMs: 300, lobbyPauseMs: 1000, minIterationMs: 1000 });

/** Default ramp profile used when RAMP=1 and no STAGES are given: 100 -> 500 -> 1000 players. */
const DEFAULT_RAMP_STAGES = [
  { duration: '1m', target: 100 },
  { duration: '2m', target: 100 },
  { duration: '1m', target: 500 },
  { duration: '2m', target: 500 },
  { duration: '1m', target: 1000 },
  { duration: '3m', target: 1000 },
  { duration: '1m', target: 0 },
];

/** Picks the executor configuration: constant VUs, or a ramp when RAMP=1 / STAGES is set. */
function loadProfile() {
  const customStages = parseStages(envString('STAGES', ''));
  if (customStages || envBool('RAMP', false)) {
    return { stages: customStages || DEFAULT_RAMP_STAGES };
  }
  return { vus: envInt('VUS', 50), duration: envString('DURATION', '2m') };
}

const p95Ms = envInt('P95_MS', 500);
const maxErrorRate = parseFloat(envString('MAX_ERROR_RATE', '0.01'));

export const options = Object.assign(loadProfile(), {
  summaryTrendStats: SUMMARY_TREND_STATS,
  userAgent: 'BlastScale-k6/1.0 (scenario)',
  thresholds: {
    http_req_failed: [`rate<${maxErrorRate}`],
    http_req_duration: [`p(95)<${p95Ms}`],
    'http_req_duration{name:POST /api/v1/levels/{level}/complete}': [`p(95)<${p95Ms * 2}`],
    errors: [`rate<${maxErrorRate}`],
    checks: ['rate>0.99'],
    levels_completed: ['count>0'],
  },
});

/** Verifies the API is reachable, logs the effective settings and hands the run id to the VUs. */
export function setup() {
  return prepareRun(SCENARIO, settings);
}

/** One iteration = one level of one player (plus the app launch the first time). */
export default function (data) {
  gameLoop(buildContext(data, settings));
}

/** Prints the compact report and writes the JSON summary when RESULTS_DIR is set. */
export function handleSummary(data) {
  return buildSummary(data, SCENARIO);
}
