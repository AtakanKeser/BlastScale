/**
 * End-of-test summary shared by the scripts.
 *
 * Exporting handleSummary() replaces k6's built-in report, so this module prints its own compact
 * one (RPS, p50/p95/p99, error rate, gameplay outcomes, per-step latency, thresholds) and, when
 * RESULTS_DIR is set, also writes the complete k6 summary object to
 * `${RESULTS_DIR}/summary-<scenario>-<timestamp>.json` (docker compose mounts ./load-test/results
 * at /results).
 */

/** Looks up a metric in the summary data, returning null when it never received a sample. */
function metric(data, name) {
  const m = data && data.metrics ? data.metrics[name] : null;
  return m && m.values ? m : null;
}

/** Formats milliseconds with a sensible unit. */
function fmtMs(value) {
  if (value === undefined || value === null || Number.isNaN(value)) {
    return '-';
  }
  if (value >= 1000) {
    return `${(value / 1000).toFixed(2)}s`;
  }
  return `${value.toFixed(value < 10 ? 2 : 1)}ms`;
}

/** Formats a 0..1 rate as a percentage. */
function pct(rate) {
  return rate === undefined || rate === null ? '-' : `${(rate * 100).toFixed(2)}%`;
}

/** Formats a test duration (ms) as 1m23s. */
function fmtDuration(ms) {
  const total = Math.round(ms / 1000);
  const minutes = Math.floor(total / 60);
  const seconds = total % 60;
  return minutes > 0 ? `${minutes}m${String(seconds).padStart(2, '0')}s` : `${seconds}s`;
}

/** Count of a Counter metric (0 when it never fired). */
function count(data, name) {
  const m = metric(data, name);
  return m ? m.values.count || 0 : 0;
}

/** One line of percentiles for a Trend metric. */
function trendLine(m) {
  const v = m.values;
  return `p50 ${fmtMs(v.med)}  p90 ${fmtMs(v['p(90)'])}  p95 ${fmtMs(v['p(95)'])}  p99 ${fmtMs(v['p(99)'])}  max ${fmtMs(v.max)}  avg ${fmtMs(v.avg)}`;
}

/** Collects every check with at least one failure, walking nested groups. */
function failedChecks(group, acc) {
  if (!group) {
    return acc;
  }
  const checks = Array.isArray(group.checks) ? group.checks : Object.values(group.checks || {});
  for (const c of checks) {
    if (c.fails > 0) {
      acc.push(c);
    }
  }
  const groups = Array.isArray(group.groups) ? group.groups : Object.values(group.groups || {});
  for (const g of groups) {
    failedChecks(g, acc);
  }
  return acc;
}

/** Lines describing every threshold and whether it passed. */
function thresholdLines(data) {
  const lines = [];
  for (const name of Object.keys(data.metrics || {})) {
    const m = data.metrics[name];
    if (!m.thresholds) {
      continue;
    }
    for (const expr of Object.keys(m.thresholds)) {
      const ok = m.thresholds[expr].ok;
      lines.push(`  ${ok ? 'PASS' : 'FAIL'}  ${name} ${expr}`);
    }
  }
  return lines;
}

/** Renders the compact human readable report. */
export function textReport(data, scenarioName) {
  const lines = [];
  const durationMs = data.state ? data.state.testRunDurationMs : 0;
  const reqs = metric(data, 'http_reqs');
  const failed = metric(data, 'http_req_failed');
  const duration = metric(data, 'http_req_duration');
  const iterations = metric(data, 'iterations');
  const vusMax = metric(data, 'vus_max');
  const checks = metric(data, 'checks');
  const errors = metric(data, 'errors');

  lines.push('');
  lines.push(`==== BlastScale load test: ${scenarioName} ====`);
  lines.push(
    `run:        ${fmtDuration(durationMs)} | max VUs ${vusMax ? vusMax.values.max : '-'} | ` +
      `iterations ${iterations ? iterations.values.count : 0} (${iterations ? iterations.values.rate.toFixed(2) : '0'}/s)`,
  );
  lines.push(
    `http:       ${reqs ? reqs.values.count : 0} requests, ${reqs ? reqs.values.rate.toFixed(1) : '0'} req/s | ` +
      `failed ${failed ? pct(failed.values.rate) : '-'} (${failed ? failed.values.passes : 0})`,
  );
  if (duration) {
    lines.push(`latency:    ${trendLine(duration)}`);
  }
  lines.push(
    `checks:     ${checks ? pct(checks.values.rate) : '-'} passed (${checks ? checks.values.passes : 0} ok, ${checks ? checks.values.fails : 0} failed) | ` +
      `unexpected step errors ${errors ? pct(errors.values.rate) : '-'}`,
  );
  lines.push(
    `gameplay:   levels completed ${count(data, 'levels_completed')}, target missed ${count(data, 'levels_failed')}, ` +
      `completions rejected ${count(data, 'completions_rejected')}, replayed ${count(data, 'completions_replayed')}`,
  );
  lines.push(
    `players:    created ${count(data, 'players_created')}, out of lives ${count(data, 'no_lives_left')}, ` +
      `business rejections ${count(data, 'business_rejections')}, rate limited ${count(data, 'rate_limited')}, retries ${count(data, 'retries')}`,
  );
  const solver = metric(data, 'solver_time');
  const moves = metric(data, 'level_moves');
  const think = metric(data, 'think_time');
  if (solver && moves) {
    lines.push(
      `solver:     avg ${fmtMs(solver.values.avg)} per level, ${moves.values.avg.toFixed(1)} moves per level` +
        (think ? `, think time avg ${fmtMs(think.values.avg)}` : ''),
    );
  }

  const stepNames = Object.keys(data.metrics || {}).filter((n) => n.startsWith('step_')).sort();
  if (stepNames.length > 0) {
    lines.push('steps (server round trip):');
    for (const name of stepNames) {
      const m = metric(data, name);
      if (m) {
        lines.push(`  ${name.replace('step_', '').padEnd(16)} ${trendLine(m)}`);
      }
    }
  }

  const thresholds = thresholdLines(data);
  if (thresholds.length > 0) {
    lines.push('thresholds:');
    lines.push(...thresholds);
  }

  const failing = failedChecks(data.root_group, []);
  if (failing.length > 0) {
    lines.push('failed checks:');
    for (const c of failing) {
      lines.push(`  ${c.name}: ${c.fails} failed / ${c.passes} passed`);
    }
  }
  lines.push('');
  return lines.join('\n');
}

/** Builds the handleSummary() result: stdout text plus the optional JSON file. */
export function buildSummary(data, scenarioName) {
  const result = { stdout: textReport(data, scenarioName) };
  const dir = __ENV.RESULTS_DIR;
  if (dir) {
    const timestamp = new Date().toISOString().replace(/[-:]/g, '').replace(/\.\d+Z$/, 'Z');
    const file = `${dir.replace(/\/+$/, '')}/summary-${scenarioName}-${timestamp}.json`;
    result[file] = JSON.stringify(data, null, 2);
    result.stdout += `summary written to ${file}\n`;
  }
  return result;
}
