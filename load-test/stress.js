/**
 * Stress test: ramps the number of players up to 2000 to find the breaking point.
 *
 * Same flow as scenario.js, but with the shortest think time the server still accepts
 * (160 ms per move, the anti-cheat minimum is 150) and only a short pause between levels, so
 * every player generates as many requests as a human plausibly could. There are no pass/fail
 * thresholds: watch the summary (and Grafana) for the VU count at which latency or the error rate
 * takes off.
 *
 *   STAGES=1m:200,2m:1000,2m:2000,1m:0   custom ramp; MAX_VUS=2000 scales the default ramp
 *   docker compose --profile loadtest run --rm k6 run /scripts/stress.js
 */
import { SUMMARY_TREND_STATS, envInt, envString, parseStages } from './config.js';
import { buildContext, buildSettings, gameLoop, prepareRun } from './flow.js';
import { buildSummary } from './summary.js';

const SCENARIO = 'stress';

/** Just above the server's 150 ms per move minimum, short lobby pause, one second pacing floor. */
const settings = buildSettings({ thinkMsPerMove: 160, thinkBaseMs: 300, lobbyPauseMs: 250, minIterationMs: 1000 });

/** Default ramp: 100 -> 500 -> 1000 -> 1500 -> 2000 players, hold, then ramp down. */
function defaultStages() {
  const max = envInt('MAX_VUS', 2000);
  const step = (fraction) => Math.max(1, Math.round(max * fraction));
  return [
    { duration: '1m', target: step(0.05) },
    { duration: '2m', target: step(0.25) },
    { duration: '2m', target: step(0.5) },
    { duration: '2m', target: step(0.75) },
    { duration: '2m', target: max },
    { duration: '3m', target: max },
    { duration: '1m', target: 0 },
  ];
}

export const options = {
  stages: parseStages(envString('STAGES', '')) || defaultStages(),
  summaryTrendStats: SUMMARY_TREND_STATS,
  userAgent: 'BlastScale-k6/1.0 (stress)',
  // Observation only: no thresholds, the run never "fails", the numbers tell the story.
  thresholds: {},
};

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
