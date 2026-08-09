/**
 * Smoke test: one virtual player runs the full flow a few times with strict checks.
 *
 * Use it to validate a deployment before any real load: every call must succeed, every level
 * must be accepted by the server-side replay. Everything is logged step by step.
 *
 *   docker compose --profile loadtest run --rm k6 run /scripts/smoke.js
 *   k6 run -e BASE_URL=http://localhost:8080 smoke.js
 */
import { SUMMARY_TREND_STATS, envInt } from './config.js';
import { buildContext, buildSettings, gameLoop, prepareRun } from './flow.js';
import { buildSummary } from './summary.js';

const SCENARIO = 'smoke';

/** Verbose by default and a short pause between levels; think time stays at the human default. */
const settings = buildSettings({ verbose: true, lobbyPauseMs: 200, minIterationMs: 0 });

export const options = {
  vus: 1,
  iterations: envInt('ITERATIONS', 3),
  summaryTrendStats: SUMMARY_TREND_STATS,
  userAgent: 'BlastScale-k6/1.0 (smoke)',
  thresholds: {
    // Strict: nothing may fail, every completion must be accepted, at least one level must be won.
    http_req_failed: ['rate==0'],
    checks: ['rate==1'],
    errors: ['rate==0'],
    completions_rejected: ['count==0'],
    levels_completed: ['count>0'],
  },
};

/** Verifies the API is reachable and hands the run id to the VU. */
export function setup() {
  return prepareRun(SCENARIO, settings);
}

/** One iteration = launch (first time only) and one level with wallet and leaderboard refresh. */
export default function (data) {
  gameLoop(buildContext(data, settings));
}

/** Prints the compact report and writes the JSON summary when RESULTS_DIR is set. */
export function handleSummary(data) {
  return buildSummary(data, SCENARIO);
}
