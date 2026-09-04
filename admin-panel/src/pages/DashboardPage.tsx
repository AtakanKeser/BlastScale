import { Link } from 'react-router-dom';
import * as api from '../api';
import { PROMETHEUS_URL, PROM_QUERIES, scalar, vectorBy } from '../prometheus';
import { usePolling } from '../hooks/useAsync';
import { EmptyRow, ErrorBox, Section, StatCard, StatusBadge } from '../components/ui';
import { entriesText, formatDateTime, formatDuration, formatMillis, formatNumber, formatPercent, sumValues } from '../format';

const REFRESH_MS = 10_000;

/** Values of the Prometheus header cards; null means "no data" (no traffic yet, NaN, or Prometheus down). */
interface PromMetrics {
  reachable: boolean;
  requestsPerSecond: number | null;
  p95Millis: number | null;
  p99Millis: number | null;
  errorRate: number | null;
  cacheHitRate: number | null;
  completionsPerMinute: Record<string, number> | null;
}

/** Unwraps a settled promise, treating a rejection as "no value". */
function settledValue<T>(result: PromiseSettledResult<T>): T | null {
  return result.status === 'fulfilled' ? result.value : null;
}

/** Runs all header-card queries in parallel; each degrades to null on its own so one bad query never blanks the row. */
async function loadPromMetrics(): Promise<PromMetrics> {
  const settled = await Promise.allSettled([
    scalar(PROM_QUERIES.requestsPerSecond),
    scalar(PROM_QUERIES.p95Seconds),
    scalar(PROM_QUERIES.p99Seconds),
    scalar(PROM_QUERIES.errorRate),
    scalar(PROM_QUERIES.cacheHitRate),
    vectorBy(PROM_QUERIES.completionsPerMinute, 'result'),
  ]);
  const [rps, p95, p99, errorRate, cacheHitRate, completions] = settled;
  const p95Seconds = settledValue(p95);
  const p99Seconds = settledValue(p99);
  return {
    reachable: settled.some((result) => result.status === 'fulfilled'),
    requestsPerSecond: settledValue(rps),
    p95Millis: p95Seconds === null ? null : p95Seconds * 1000,
    p99Millis: p99Seconds === null ? null : p99Seconds * 1000,
    errorRate: settledValue(errorRate),
    cacheHitRate: settledValue(cacheHitRate),
    completionsPerMinute: settledValue(completions),
  };
}

/** Dashboard: Prometheus traffic cards + business counters from /api/v1/admin/dashboard, refreshed every 10s. */
export default function DashboardPage() {
  const prom = usePolling(loadPromMetrics, REFRESH_MS);
  const dash = usePolling(() => api.getDashboard(), REFRESH_MS);
  const events = usePolling(() => api.listEvents(), 30_000);
  const experiments = usePolling(() => api.listExperiments(), 30_000);

  const m = prom.data;
  const d = dash.data;
  const completionsTotal = sumValues(m?.completionsPerMinute);
  const liveEvents = (events.data ?? []).filter((e) => e.status === 'ACTIVE' || e.status === 'SCHEDULED').slice(0, 5);
  const liveExperiments = (experiments.data ?? [])
    .filter((e) => e.status === 'RUNNING' || e.status === 'PAUSED')
    .slice(0, 5);

  return (
    <div>
      <div className="page-header">
        <h1>Dashboard</h1>
        <span className="muted">
          Auto-refresh every 10s
          {dash.updatedAt ? ` · last update ${new Date(dash.updatedAt).toLocaleTimeString()}` : ''}
        </span>
      </div>

      <div className="subheading">Traffic (Prometheus, 1 minute window)</div>
      {m && !m.reachable && (
        <div className="hint">Prometheus is unreachable at {PROMETHEUS_URL}; traffic cards show n/a.</div>
      )}
      <div className="cards">
        <StatCard label="Requests / sec" value={formatNumber(m?.requestsPerSecond, 2)} />
        <StatCard label="p95 latency" value={formatMillis(m?.p95Millis)} />
        <StatCard label="p99 latency" value={formatMillis(m?.p99Millis)} />
        <StatCard label="Error rate (5xx)" value={formatPercent(m?.errorRate)} />
        <StatCard label="Redis cache hit rate" value={formatPercent(m?.cacheHitRate, 1)} hint="5 minute window" />
        <StatCard
          label="Level completions / min"
          value={formatNumber(completionsTotal, 1)}
          hint={entriesText(m?.completionsPerMinute, 1) || 'by result'}
        />
      </div>

      <div className="subheading">Platform (API instance, since process start)</div>
      <ErrorBox error={dash.error} />
      <div className="cards">
        <StatCard label="Players" value={formatNumber(d?.players)} />
        <StatCard label="Level starts" value={formatNumber(d?.levelStarts)} />
        <StatCard
          label="Level completions"
          value={formatNumber(sumValues(d?.levelCompletions))}
          hint={entriesText(d?.levelCompletions) || 'by result'}
        />
        <StatCard
          label="Rejections by validator"
          value={formatNumber(sumValues(d?.completionRejections))}
          hint={entriesText(d?.completionRejections) || 'no rejections'}
        />
        <StatCard
          label="Outbox pending"
          value={formatNumber(d?.outbox.pending)}
          hint={`dead-lettered: ${formatNumber(d?.outbox.deadLettered)}`}
        />
        <StatCard label="Active events" value={formatNumber(d?.activeEvents)} />
        <StatCard label="Running experiments" value={formatNumber(d?.runningExperiments)} />
        <StatCard label="Uptime" value={formatDuration(d?.uptimeSeconds)} />
        <StatCard
          label="HTTP requests"
          value={formatNumber(d?.http.requests)}
          hint={
            d
              ? `mean ${formatMillis(d.http.meanLatencyMillis)} · max ${formatMillis(d.http.maxLatencyMillis)} · 5xx ${formatPercent(d.http.serverErrorRate)}`
              : undefined
          }
        />
        <StatCard label="Cache hit rate" value={formatPercent(d?.cacheHitRate, 1)} hint="in-process counters" />
      </div>

      <div className="two-columns">
        <Section title="Live events" actions={<Link to="/events">Manage</Link>}>
          <ErrorBox error={events.error} />
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Type</th>
                <th>Status</th>
                <th>Ends</th>
                <th className="num">Participants</th>
              </tr>
            </thead>
            <tbody>
              {liveEvents.length === 0 ? (
                <EmptyRow colSpan={5} text="No active or scheduled events" />
              ) : (
                liveEvents.map((e) => (
                  <tr key={e.id}>
                    <td>{e.name}</td>
                    <td>{e.type}</td>
                    <td>
                      <StatusBadge status={e.status} />
                    </td>
                    <td>{formatDateTime(e.endAt)}</td>
                    <td className="num">{formatNumber(e.participants)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </Section>

        <Section title="Experiments" actions={<Link to="/experiments">Manage</Link>}>
          <ErrorBox error={experiments.error} />
          <table>
            <thead>
              <tr>
                <th>Key</th>
                <th>Status</th>
                <th>Variants</th>
              </tr>
            </thead>
            <tbody>
              {liveExperiments.length === 0 ? (
                <EmptyRow colSpan={3} text="No running experiments" />
              ) : (
                liveExperiments.map((e) => (
                  <tr key={e.id}>
                    <td>
                      {e.key}
                      <div className="muted">{e.name}</div>
                    </td>
                    <td>
                      <StatusBadge status={e.status} />
                    </td>
                    <td>{e.variants.map((v) => `${v.name} ${v.weight}%`).join(' · ')}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </Section>
      </div>
    </div>
  );
}
