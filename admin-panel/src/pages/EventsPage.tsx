import { Fragment, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import * as api from '../api';
import { useAsync } from '../hooks/useAsync';
import { useToast } from '../components/Toast';
import { EmptyRow, ErrorBox, JsonCode, Loading, Section, StatusBadge } from '../components/ui';
import { formatDateTime, formatNumber, localInputToIso, prettyJson, toDatetimeLocal } from '../format';
import { LIVE_EVENT_TYPES, type LiveEventType, type LiveEventView, type Standing } from '../types';

/** Starting configuration per event type (rule shapes from event/EventRule.java and EventRuleParser). */
const TEMPLATES: Record<LiveEventType, Record<string, unknown>> = {
  ROCKET_RACE: {
    pointsPerLevel: 1,
    minimumLevel: 5,
    rewards: { '1': 10000, '2': 5000, '3': 2500, '4': 1000, '5': 1000, '6': 500, '7': 500, '8': 500, '9': 500, '10': 500 },
  },
  DOUBLE_REWARD: { multiplier: 2.0 },
};

type EventAction = 'activate' | 'end' | 'cancel';

const ACTION_LABEL: Record<EventAction, string> = { activate: 'Activate', end: 'End', cancel: 'Cancel' };

const ACTION_FN: Record<EventAction, (id: number) => Promise<LiveEventView>> = {
  activate: api.activateEvent,
  end: api.endEvent,
  cancel: api.cancelEvent,
};

/** Transitions the backend accepts from each status (see LiveEventService.activate/end/cancel). */
function allowedActions(status: string): EventAction[] {
  const actions: EventAction[] = [];
  if (status === 'SCHEDULED') actions.push('activate');
  if (status === 'ACTIVE') actions.push('end');
  if (status !== 'FINALIZED' && status !== 'CANCELLED') actions.push('cancel');
  return actions;
}

/** "1. alice (12) · 2. bob (9) · 3. carol (7)" for the table's top-3 column. */
function topThree(top: Standing[] | null | undefined): string {
  if (!top || top.length === 0) return '—';
  return top
    .slice(0, 3)
    .map((s) => `${s.rank}. ${s.name} (${formatNumber(s.points)})`)
    .join(' · ');
}

/** Live events table with lifecycle buttons, expandable standings, and the create form. */
export default function EventsPage() {
  const toast = useToast();
  const events = useAsync(() => api.listEvents(), []);
  const [expanded, setExpanded] = useState<number | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);

  async function run(id: number, action: EventAction) {
    const prompts: Record<EventAction, string> = {
      activate: `Activate event ${id} now?`,
      end: `End event ${id} now? Prizes are paid immediately.`,
      cancel: `Cancel event ${id}? No prizes will be paid. This cannot be undone.`,
    };
    if (!window.confirm(prompts[action])) return;
    setBusyId(id);
    try {
      const updated = await ACTION_FN[action](id);
      toast.success(`Event ${updated.id} "${updated.name}" is now ${updated.status}`);
      events.reload();
    } catch (err) {
      toast.error(err);
    } finally {
      setBusyId(null);
    }
  }

  const rows = events.data ?? [];

  return (
    <div>
      <div className="page-header">
        <h1>Live events</h1>
        <button type="button" onClick={events.reload}>
          Refresh
        </button>
      </div>

      <Section title="Create event">
        <CreateEventForm onCreated={events.reload} />
      </Section>

      <Section title={`Events (${rows.length})`}>
        <ErrorBox error={events.error} />
        {events.loading && !events.data ? (
          <Loading />
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Id</th>
                  <th>Name</th>
                  <th>Type</th>
                  <th>Status</th>
                  <th>Start</th>
                  <th>End</th>
                  <th className="num">Participants</th>
                  <th>Top 3</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 ? (
                  <EmptyRow colSpan={9} text="No events yet" />
                ) : (
                  rows.map((e) => (
                    <Fragment key={e.id}>
                      <tr className="clickable" onClick={() => setExpanded(expanded === e.id ? null : e.id)}>
                        <td>{e.id}</td>
                        <td>{e.name}</td>
                        <td>{e.type}</td>
                        <td>
                          <StatusBadge status={e.status} />
                        </td>
                        <td>{formatDateTime(e.startAt)}</td>
                        <td>{formatDateTime(e.endAt)}</td>
                        <td className="num">{formatNumber(e.participants)}</td>
                        <td>{topThree(e.top)}</td>
                        <td>
                          <div className="actions">
                            {allowedActions(e.status).map((action) => (
                              <button
                                key={action}
                                type="button"
                                className={`btn-small${action === 'cancel' ? ' btn-danger' : ''}`}
                                disabled={busyId === e.id}
                                onClick={(ev) => {
                                  ev.stopPropagation();
                                  void run(e.id, action);
                                }}
                              >
                                {ACTION_LABEL[action]}
                              </button>
                            ))}
                          </div>
                        </td>
                      </tr>
                      {expanded === e.id && (
                        <tr>
                          <td colSpan={9}>
                            <div className="two-columns">
                              <div>
                                <strong>Standings</strong>
                                <StandingsTable top={e.top} />
                              </div>
                              <div>
                                <strong>Configuration</strong>
                                <div>
                                  <JsonCode value={e.configuration} />
                                </div>
                                <div className="muted">
                                  created {formatDateTime(e.createdAt)} · updated {formatDateTime(e.updatedAt)}
                                </div>
                              </div>
                            </div>
                          </td>
                        </tr>
                      )}
                    </Fragment>
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

/** Current top standings of one event (up to 10 rows, as returned by the admin list). */
function StandingsTable({ top }: { top: Standing[] | null | undefined }) {
  if (!top || top.length === 0) return <div className="muted">No participants yet</div>;
  return (
    <table>
      <thead>
        <tr>
          <th className="num">Rank</th>
          <th>Player</th>
          <th className="num">Points</th>
          <th className="num">Reward coins</th>
        </tr>
      </thead>
      <tbody>
        {top.map((s) => (
          <tr key={s.playerId}>
            <td className="num">{s.rank}</td>
            <td>
              <Link to={`/players/${s.playerId}`}>{s.name}</Link> <span className="muted">#{s.playerId}</span>
            </td>
            <td className="num">{formatNumber(s.points)}</td>
            <td className="num">{formatNumber(s.rewardCoins)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

/** Create form: type, name, optional start (empty = now), required end, and the rule JSON prefilled per type. */
function CreateEventForm({ onCreated }: { onCreated: () => void }) {
  const toast = useToast();
  const [type, setType] = useState<LiveEventType>('ROCKET_RACE');
  const [name, setName] = useState('');
  const [startAt, setStartAt] = useState('');
  const [endAt, setEndAt] = useState(() => toDatetimeLocal(new Date(Date.now() + 48 * 3600 * 1000)));
  const [config, setConfig] = useState(() => prettyJson(TEMPLATES.ROCKET_RACE));
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function changeType(next: LiveEventType) {
    setType(next);
    setConfig(prettyJson(TEMPLATES[next]));
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);

    let configuration: Record<string, unknown>;
    try {
      const parsed: unknown = JSON.parse(config);
      if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error('not an object');
      configuration = parsed as Record<string, unknown>;
    } catch {
      setError('Configuration must be a JSON object');
      return;
    }
    const endIso = localInputToIso(endAt);
    if (!endIso) {
      setError('End time is required');
      return;
    }
    const startIso = localInputToIso(startAt);
    if (new Date(endIso).getTime() <= (startIso ? new Date(startIso).getTime() : Date.now())) {
      setError('End must be after the start (or after now when starting immediately)');
      return;
    }

    setBusy(true);
    try {
      const created = await api.createEvent({ type, name: name.trim(), startAt: startIso, endAt: endIso, configuration });
      toast.success(`Created event ${created.id} "${created.name}" (${created.status})`);
      setName('');
      onCreated();
    } catch (err) {
      toast.error(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit} className="form-grid">
      <div>
        <label htmlFor="ev-type">Type</label>
        <select id="ev-type" value={type} onChange={(e) => changeType(e.target.value as LiveEventType)}>
          {LIVE_EVENT_TYPES.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
      </div>
      <div>
        <label htmlFor="ev-name">Name</label>
        <input id="ev-name" value={name} maxLength={128} onChange={(e) => setName(e.target.value)} required style={{ width: '100%' }} />
      </div>
      <div>
        <label htmlFor="ev-start">Start (empty = now)</label>
        <input id="ev-start" type="datetime-local" value={startAt} onChange={(e) => setStartAt(e.target.value)} />
      </div>
      <div>
        <label htmlFor="ev-end">End (required)</label>
        <input id="ev-end" type="datetime-local" value={endAt} onChange={(e) => setEndAt(e.target.value)} required />
      </div>
      <div className="full">
        <label htmlFor="ev-config">
          Configuration JSON —{' '}
          {type === 'ROCKET_RACE'
            ? 'pointsPerLevel (> 0), minimumLevel, rewards: rank -> coins (at least one)'
            : 'multiplier in (1, 10]'}
        </label>
        <textarea id="ev-config" value={config} onChange={(e) => setConfig(e.target.value)} spellCheck={false} />
      </div>
      <div className="full form-footer">
        <button type="submit" className="btn-primary" disabled={busy}>
          {busy ? 'Creating…' : 'Create event'}
        </button>
        {error && <span className="error-box" style={{ margin: 0 }}>{error}</span>}
      </div>
    </form>
  );
}
