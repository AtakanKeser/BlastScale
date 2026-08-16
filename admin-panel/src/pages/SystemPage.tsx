import * as api from '../api';
import { PROMETHEUS_URL } from '../prometheus';
import { useAsync, usePolling } from '../hooks/useAsync';
import type { AsyncState } from '../hooks/useAsync';
import { EmptyRow, ErrorBox, JsonCode, Loading, Section, StatCard, StatusBadge } from '../components/ui';
import { formatNumber } from '../format';
import type { Health } from '../types';

const REFRESH_MS = 10_000;

interface ComponentRow {
  name: string;
  status: string;
  details?: Record<string, unknown>;
}

/** Flattens /actuator/health components (one level of nesting for grouped indicators) into table rows. */
function flattenComponents(health: Health | null): ComponentRow[] {
  if (!health?.components) return [];
  const rows: ComponentRow[] = [];
  for (const [name, component] of Object.entries(health.components)) {
    rows.push({ name, status: component.status, details: component.details });
    if (component.components) {
      for (const [sub, nested] of Object.entries(component.components)) {
        rows.push({ name: `${name}.${sub}`, status: nested.status, details: nested.details });
      }
    }
  }
  return rows;
}

/** Stat card for one health endpoint: the status badge, or the error when the endpoint is unreachable. */
function ProbeCard({ label, state }: { label: string; state: AsyncState<Health> }) {
  const status = state.data?.status;
  return (
    <StatCard
      label={label}
      value={status ? <StatusBadge status={status} /> : state.loading ? '…' : <StatusBadge status="UNREACHABLE" />}
      hint={state.error ? <span style={{ color: 'var(--red-fg)' }}>{state.error.message}</span> : undefined}
    />
  );
}

/** System page: actuator health per component, probes, outbox backlog, anti-cheat chain and metric links. */
export default function SystemPage() {
  const health = usePolling(() => api.getHealth(), REFRESH_MS);
  const liveness = usePolling(() => api.getLiveness(), REFRESH_MS);
  const readiness = usePolling(() => api.getReadiness(), REFRESH_MS);
  const outbox = usePolling(() => api.getOutboxStats(), REFRESH_MS);
  const validators = useAsync(() => api.getValidators(), []);

  const components = flattenComponents(health.data);
  const prometheusScrapeUrl = `${api.API_BASE_URL}/actuator/prometheus`;

  return (
    <div>
      <div className="page-header">
        <h1>System</h1>
        <span className="muted">
          Auto-refresh every 10s
          {health.updatedAt ? ` · last update ${new Date(health.updatedAt).toLocaleTimeString()}` : ''}
        </span>
      </div>

      <div className="cards">
        <ProbeCard label="Overall health" state={health} />
        <ProbeCard label="Liveness" state={liveness} />
        <ProbeCard label="Readiness (MySQL only)" state={readiness} />
        <StatCard
          label="Outbox pending"
          value={formatNumber(outbox.data?.pending)}
          hint={outbox.error ? <span style={{ color: 'var(--red-fg)' }}>{outbox.error.message}</span> : 'events waiting for Elasticsearch'}
        />
        <StatCard
          label="Outbox dead-lettered"
          value={formatNumber(outbox.data?.deadLettered)}
          hint="pending rows past max attempts"
        />
      </div>

      <div className="two-columns">
        <Section title="Health components (/actuator/health)">
          <ErrorBox error={health.error} />
          {health.loading && !health.data ? (
            <Loading />
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Component</th>
                    <th>Status</th>
                    <th>Details</th>
                  </tr>
                </thead>
                <tbody>
                  {components.length === 0 ? (
                    <EmptyRow colSpan={3} text="No component details (is show-details enabled?)" />
                  ) : (
                    components.map((c) => (
                      <tr key={c.name}>
                        <td>
                          <code>{c.name}</code>
                        </td>
                        <td>
                          <StatusBadge status={c.status} />
                        </td>
                        <td>{c.details ? <JsonCode value={c.details} /> : <span className="muted">—</span>}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          )}
        </Section>

        <div>
          <Section title="Anti-cheat validators (execution order)">
            <ErrorBox error={validators.error} />
            {validators.loading && !validators.data ? (
              <Loading />
            ) : (
              <ol className="inline-list">
                {(validators.data ?? []).map((name) => (
                  <li key={name}>
                    <code>{name}</code>
                  </li>
                ))}
              </ol>
            )}
          </Section>

          <Section title="Metrics">
            <dl className="props">
              <dt>Prometheus scrape</dt>
              <dd>
                <a href={prometheusScrapeUrl} target="_blank" rel="noreferrer">
                  {prometheusScrapeUrl}
                </a>
              </dd>
              <dt>Prometheus UI</dt>
              <dd>
                <a href={`${PROMETHEUS_URL}/graph`} target="_blank" rel="noreferrer">
                  {PROMETHEUS_URL}
                </a>
              </dd>
              <dt>Targets</dt>
              <dd>
                <a href={`${PROMETHEUS_URL}/targets`} target="_blank" rel="noreferrer">
                  {PROMETHEUS_URL}/targets
                </a>
              </dd>
            </dl>
          </Section>
        </div>
      </div>
    </div>
  );
}
