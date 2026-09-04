import { useState, type FormEvent } from 'react';
import * as api from '../api';
import { useAsync } from '../hooks/useAsync';
import { useToast } from '../components/Toast';
import { EmptyRow, ErrorBox, JsonCode, Loading, Section, StatusBadge } from '../components/ui';
import { formatDateTime, formatNumber, localInputToIso } from '../format';
import type { ExperimentVariant, ExperimentView } from '../types';

type ExperimentAction = 'start' | 'pause' | 'end';

const ACTION_LABEL: Record<ExperimentAction, string> = { start: 'Start', pause: 'Pause', end: 'End' };

const ACTION_FN: Record<ExperimentAction, (id: number) => Promise<ExperimentView>> = {
  start: api.startExperiment,
  pause: api.pauseExperiment,
  end: api.endExperiment,
};

/** Transitions the backend accepts per status (ExperimentService.transition): DRAFT -> RUNNING <-> PAUSED -> ENDED. */
function allowedActions(status: string): ExperimentAction[] {
  const actions: ExperimentAction[] = [];
  if (status === 'DRAFT' || status === 'PAUSED') actions.push('start');
  if (status === 'RUNNING') actions.push('pause');
  if (status !== 'ENDED') actions.push('end');
  return actions;
}

/** Experiments table with lifecycle buttons plus the create form with a variants editor. */
export default function ExperimentsPage() {
  const toast = useToast();
  const experiments = useAsync(() => api.listExperiments(), []);
  const [busyId, setBusyId] = useState<number | null>(null);

  async function run(id: number, action: ExperimentAction) {
    if (action === 'end' && !window.confirm(`End experiment ${id}? It cannot be restarted.`)) return;
    setBusyId(id);
    try {
      const updated = await ACTION_FN[action](id);
      toast.success(`Experiment "${updated.key}" is now ${updated.status}`);
      experiments.reload();
    } catch (err) {
      toast.error(err);
    } finally {
      setBusyId(null);
    }
  }

  const rows = experiments.data ?? [];

  return (
    <div>
      <div className="page-header">
        <h1>Experiments</h1>
        <button type="button" onClick={experiments.reload}>
          Refresh
        </button>
      </div>

      <Section title="Create experiment">
        <CreateExperimentForm onCreated={experiments.reload} />
      </Section>

      <Section title={`Experiments (${rows.length})`}>
        <p className="muted">
          Only RUNNING experiments inside their optional time window assign players. Players are bucketed by a
          hash of (player id, key), so an assignment is stable for the lifetime of the experiment.
        </p>
        <ErrorBox error={experiments.error} />
        {experiments.loading && !experiments.data ? (
          <Loading />
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Id</th>
                  <th>Key</th>
                  <th>Name</th>
                  <th>Status</th>
                  <th>Window</th>
                  <th>Variants (weight, overrides)</th>
                  <th>Assignments</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 ? (
                  <EmptyRow colSpan={8} text="No experiments yet" />
                ) : (
                  rows.map((e) => (
                    <tr key={e.id}>
                      <td>{e.id}</td>
                      <td>
                        <code>{e.key}</code>
                      </td>
                      <td>{e.name}</td>
                      <td>
                        <StatusBadge status={e.status} />
                      </td>
                      <td>
                        {e.startAt || e.endAt ? (
                          <>
                            {formatDateTime(e.startAt)} → {formatDateTime(e.endAt)}
                          </>
                        ) : (
                          <span className="muted">no window</span>
                        )}
                        <div className="muted">created {formatDateTime(e.createdAt)}</div>
                      </td>
                      <td>
                        {e.variants.map((v) => (
                          <div key={v.name}>
                            <strong>{v.name}</strong> {v.weight}% <JsonCode value={v.overrides} />
                          </div>
                        ))}
                      </td>
                      <td>
                        {e.assignments ? (
                          Object.entries(e.assignments).map(([variant, count]) => (
                            <div key={variant}>
                              {variant}: {formatNumber(count)}
                            </div>
                          ))
                        ) : (
                          <span className="muted">—</span>
                        )}
                      </td>
                      <td>
                        <div className="actions">
                          {allowedActions(e.status).map((action) => (
                            <button
                              key={action}
                              type="button"
                              className={`btn-small${action === 'end' ? ' btn-danger' : ''}`}
                              disabled={busyId === e.id}
                              onClick={() => void run(e.id, action)}
                            >
                              {ACTION_LABEL[action]}
                            </button>
                          ))}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        )}
      </Section>
    </div>
  );
}

/** One row of the variants editor; weight and overrides stay text until validation on submit. */
interface VariantDraft {
  name: string;
  weight: string;
  overrides: string;
}

const DEFAULT_VARIANTS: VariantDraft[] = [
  { name: 'control', weight: '50', overrides: '{}' },
  { name: 'treatment', weight: '50', overrides: '{\n  "lifeRegenerationMinutes": 20\n}' },
];

/** Validates the drafts (unique names, integer weights summing to 100, overrides JSON objects). */
function parseVariants(drafts: VariantDraft[]): { variants?: ExperimentVariant[]; error?: string } {
  if (drafts.length === 0) return { error: 'At least one variant is required' };
  const names = new Set<string>();
  const variants: ExperimentVariant[] = [];
  let total = 0;
  for (const draft of drafts) {
    const name = draft.name.trim();
    if (!name) return { error: 'Every variant needs a name' };
    if (names.has(name)) return { error: `Duplicate variant name "${name}"` };
    names.add(name);
    const weight = Number(draft.weight);
    if (!Number.isInteger(weight) || weight < 0) return { error: `Weight of "${name}" must be a non-negative integer` };
    total += weight;
    let overrides: Record<string, unknown>;
    try {
      const parsed: unknown = JSON.parse(draft.overrides.trim() || '{}');
      if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error('not an object');
      overrides = parsed as Record<string, unknown>;
    } catch {
      return { error: `Overrides of "${name}" must be a JSON object, e.g. {"maxLives": 7}` };
    }
    variants.push({ name, weight, overrides });
  }
  if (total !== 100) return { error: `Variant weights must sum to 100 (currently ${total})` };
  return { variants };
}

/** Create form: key, name, optional window, and an editable list of variants. */
function CreateExperimentForm({ onCreated }: { onCreated: () => void }) {
  const toast = useToast();
  const [key, setKey] = useState('');
  const [name, setName] = useState('');
  const [startAt, setStartAt] = useState('');
  const [endAt, setEndAt] = useState('');
  const [variants, setVariants] = useState<VariantDraft[]>(DEFAULT_VARIANTS);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const weightTotal = variants.reduce((sum, v) => sum + (Number(v.weight) || 0), 0);

  function updateVariant(index: number, patch: Partial<VariantDraft>) {
    setVariants((current) => current.map((v, i) => (i === index ? { ...v, ...patch } : v)));
  }

  function removeVariant(index: number) {
    setVariants((current) => current.filter((_, i) => i !== index));
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    const trimmedKey = key.trim();
    if (!/^[a-z0-9_]+$/.test(trimmedKey)) {
      setError('Key may only contain lower case letters, digits and underscore');
      return;
    }
    const parsed = parseVariants(variants);
    if (!parsed.variants) {
      setError(parsed.error ?? 'Invalid variants');
      return;
    }
    const startIso = localInputToIso(startAt);
    const endIso = localInputToIso(endAt);
    if (startIso && endIso && new Date(endIso).getTime() <= new Date(startIso).getTime()) {
      setError('End must be after start');
      return;
    }
    setBusy(true);
    try {
      const created = await api.createExperiment({
        key: trimmedKey,
        name: name.trim(),
        variants: parsed.variants,
        startAt: startIso,
        endAt: endIso,
      });
      toast.success(`Created experiment "${created.key}" (${created.status}); start it when ready`);
      setKey('');
      setName('');
      setStartAt('');
      setEndAt('');
      setVariants(DEFAULT_VARIANTS);
      onCreated();
    } catch (err) {
      toast.error(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit}>
      <div className="form-grid">
        <div>
          <label htmlFor="ex-key">Key (a-z, 0-9, _)</label>
          <input
            id="ex-key"
            value={key}
            maxLength={64}
            onChange={(e) => setKey(e.target.value)}
            placeholder="life_regen_test"
            required
            style={{ width: '100%' }}
          />
        </div>
        <div>
          <label htmlFor="ex-name">Name</label>
          <input id="ex-name" value={name} maxLength={128} onChange={(e) => setName(e.target.value)} required style={{ width: '100%' }} />
        </div>
        <div>
          <label htmlFor="ex-start">Start (optional)</label>
          <input id="ex-start" type="datetime-local" value={startAt} onChange={(e) => setStartAt(e.target.value)} />
        </div>
        <div>
          <label htmlFor="ex-end">End (optional)</label>
          <input id="ex-end" type="datetime-local" value={endAt} onChange={(e) => setEndAt(e.target.value)} />
        </div>
      </div>

      <div className="subheading" style={{ marginTop: 14 }}>
        Variants (weights sum: {weightTotal}/100)
      </div>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th style={{ width: 110 }}>Weight %</th>
              <th>Overrides JSON (remote config key → value)</th>
              <th style={{ width: 90 }} />
            </tr>
          </thead>
          <tbody>
            {variants.map((v, index) => (
              <tr key={index}>
                <td>
                  <input value={v.name} onChange={(e) => updateVariant(index, { name: e.target.value })} placeholder="control" />
                </td>
                <td>
                  <input
                    type="number"
                    min={0}
                    max={100}
                    step={1}
                    value={v.weight}
                    onChange={(e) => updateVariant(index, { weight: e.target.value })}
                    style={{ width: 90 }}
                  />
                </td>
                <td>
                  <textarea
                    value={v.overrides}
                    onChange={(e) => updateVariant(index, { overrides: e.target.value })}
                    spellCheck={false}
                    style={{ minHeight: 60 }}
                  />
                </td>
                <td>
                  <button type="button" className="btn-small btn-danger" onClick={() => removeVariant(index)}>
                    Remove
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="form-footer">
        <button type="button" onClick={() => setVariants((current) => [...current, { name: '', weight: '0', overrides: '{}' }])}>
          Add variant
        </button>
        <button type="submit" className="btn-primary" disabled={busy}>
          {busy ? 'Creating…' : 'Create experiment'}
        </button>
        {error && <span className="error-box" style={{ margin: 0 }}>{error}</span>}
      </div>
    </form>
  );
}
