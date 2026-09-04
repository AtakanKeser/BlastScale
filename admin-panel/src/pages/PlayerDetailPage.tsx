import { useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import * as api from '../api';
import { useAsync } from '../hooks/useAsync';
import { useToast } from '../components/Toast';
import { EmptyRow, ErrorBox, JsonCode, Loading, Pager, Section, StatusBadge } from '../components/ui';
import { formatDateTime, formatNumber, localInputToIso } from '../format';
import { RESOURCES, TELEMETRY_EVENT_TYPES, type PlayerProfile, type Resource, type WalletSnapshot } from '../types';

type Tab = 'profile' | 'wallet' | 'ledger' | 'sessions' | 'progress' | 'events';

const TABS: { id: Tab; label: string }[] = [
  { id: 'profile', label: 'Profile' },
  { id: 'wallet', label: 'Wallet & grants' },
  { id: 'ledger', label: 'Ledger' },
  { id: 'sessions', label: 'Sessions' },
  { id: 'progress', label: 'Progress' },
  { id: 'events', label: 'Event timeline' },
];

/** Player detail page: profile header plus one lazily loaded section per tab. */
export default function PlayerDetailPage() {
  const { id } = useParams();
  const playerId = Number(id);
  const valid = Number.isInteger(playerId) && playerId > 0;
  const [tab, setTab] = useState<Tab>('profile');
  const profile = useAsync(
    () => (valid ? api.getPlayer(playerId) : Promise.reject(new Error(`Invalid player id '${id}'`))),
    [playerId],
  );

  return (
    <div>
      <div className="page-header">
        <h1>
          Player {valid ? playerId : '?'}
          {profile.data ? (
            <>
              {' '}
              · {profile.data.username} <StatusBadge status={profile.data.role} />
            </>
          ) : null}
        </h1>
        <Link to="/players">Back to players</Link>
      </div>
      <ErrorBox error={profile.error} />

      <div className="tabs">
        {TABS.map((t) => (
          <button
            key={t.id}
            type="button"
            className={tab === t.id ? 'tab active' : 'tab'}
            onClick={() => setTab(t.id)}
          >
            {t.label}
          </button>
        ))}
      </div>

      {valid && tab === 'profile' && <ProfileTab profile={profile.data} loading={profile.loading} />}
      {valid && tab === 'wallet' && <WalletTab playerId={playerId} onChanged={profile.reload} />}
      {valid && tab === 'ledger' && <LedgerTab playerId={playerId} />}
      {valid && tab === 'sessions' && <SessionsTab playerId={playerId} />}
      {valid && tab === 'progress' && <ProgressTab playerId={playerId} />}
      {valid && tab === 'events' && <EventsTab playerId={playerId} />}
    </div>
  );
}

/** Wallet fields as a definition list (used by the profile and wallet tabs). */
function WalletProps({ wallet }: { wallet: WalletSnapshot }) {
  const boosters = Object.entries(wallet.boosters ?? {});
  return (
    <dl className="props">
      <dt>Coins</dt>
      <dd>{formatNumber(wallet.coins)}</dd>
      <dt>Lives</dt>
      <dd>
        {wallet.lives} / {wallet.maxLives}
        {wallet.nextLifeInSeconds > 0 ? ` (next life in ${wallet.nextLifeInSeconds}s)` : ' (full)'}
      </dd>
      <dt>Stars</dt>
      <dd>{formatNumber(wallet.stars)}</dd>
      <dt>Boosters</dt>
      <dd>{boosters.length === 0 ? '—' : boosters.map(([name, count]) => `${name}: ${count}`).join(', ')}</dd>
    </dl>
  );
}

/** Profile tab: the uncached GET /admin/players/{id} read model. */
function ProfileTab({ profile, loading }: { profile: PlayerProfile | null; loading: boolean }) {
  if (loading && !profile) return <Loading />;
  if (!profile) return null;
  return (
    <div className="two-columns">
      <Section title="Profile">
        <dl className="props">
          <dt>Id</dt>
          <dd>{profile.id}</dd>
          <dt>Username</dt>
          <dd>{profile.username}</dd>
          <dt>Role</dt>
          <dd>
            <StatusBadge status={profile.role} />
          </dd>
          <dt>Current level</dt>
          <dd>{profile.currentLevel}</dd>
          <dt>Created</dt>
          <dd>{formatDateTime(profile.createdAt)}</dd>
        </dl>
      </Section>
      <Section title="Wallet summary">
        {profile.wallet ? <WalletProps wallet={profile.wallet} /> : <div className="muted">No wallet attached</div>}
      </Section>
    </div>
  );
}

/** Wallet tab: live wallet snapshot plus the "grant resources" compensation form. */
function WalletTab({ playerId, onChanged }: { playerId: number; onChanged: () => void }) {
  const toast = useToast();
  const wallet = useAsync(() => api.getWallet(playerId), [playerId]);
  const [resource, setResource] = useState<Resource>('COIN');
  const [amount, setAmount] = useState('100');
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    const value = Number(amount);
    if (!Number.isInteger(value) || value === 0) {
      toast.error('Amount must be a non-zero integer (negative removes resources)');
      return;
    }
    setBusy(true);
    try {
      await api.grantResources(playerId, { resource, amount: value, note: note.trim() || undefined });
      toast.success(`${value > 0 ? 'Granted' : 'Removed'} ${Math.abs(value)} ${resource} for player ${playerId}`);
      setNote('');
      wallet.reload();
      onChanged();
    } catch (err) {
      toast.error(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="two-columns">
      <Section title="Wallet" actions={<button type="button" onClick={wallet.reload}>Refresh</button>}>
        <ErrorBox error={wallet.error} />
        {wallet.loading && !wallet.data ? <Loading /> : wallet.data ? <WalletProps wallet={wallet.data} /> : null}
      </Section>
      <Section title="Grant resources">
        <p className="muted">
          Manual compensation, recorded in the ledger with reason ADMIN_GRANT and a telemetry event. A negative
          amount takes resources away.
        </p>
        <form onSubmit={submit} className="form-grid">
          <div>
            <label htmlFor="grant-resource">Resource</label>
            <select id="grant-resource" value={resource} onChange={(e) => setResource(e.target.value as Resource)}>
              {RESOURCES.map((r) => (
                <option key={r} value={r}>
                  {r}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="grant-amount">Amount</label>
            <input id="grant-amount" type="number" step={1} value={amount} onChange={(e) => setAmount(e.target.value)} required />
          </div>
          <div className="full">
            <label htmlFor="grant-note">Note (max 64 chars)</label>
            <input
              id="grant-note"
              value={note}
              maxLength={64}
              onChange={(e) => setNote(e.target.value)}
              placeholder="e.g. compensation for outage 2026-09-04"
              style={{ width: '100%' }}
            />
          </div>
          <div className="full">
            <button type="submit" className="btn-primary" disabled={busy}>
              {busy ? 'Applying…' : 'Apply grant'}
            </button>
          </div>
        </form>
      </Section>
    </div>
  );
}

/** Ledger tab: append-only economy transactions, newest first, with paging. */
function LedgerTab({ playerId }: { playerId: number }) {
  const [page, setPage] = useState(0);
  const size = 50;
  const result = useAsync(() => api.getTransactions(playerId, page, size), [playerId, page]);
  const rows = result.data?.content ?? [];

  return (
    <Section title={`Ledger${result.data ? ` (${result.data.totalElements} entries)` : ''}`}>
      <ErrorBox error={result.error} />
      {result.loading && !result.data ? (
        <Loading />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Id</th>
                <th>Created</th>
                <th>Type</th>
                <th>Resource</th>
                <th className="num">Amount</th>
                <th className="num">Balance after</th>
                <th>Reason</th>
                <th>Reference</th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 ? (
                <EmptyRow colSpan={8} text="No transactions" />
              ) : (
                rows.map((t) => (
                  <tr key={t.id}>
                    <td>{t.id}</td>
                    <td>{formatDateTime(t.createdAt)}</td>
                    <td>
                      <StatusBadge status={t.type} />
                    </td>
                    <td>{t.resource}</td>
                    <td className="num">{formatNumber(t.amount)}</td>
                    <td className="num">{formatNumber(t.balanceAfter)}</td>
                    <td>{t.reason}</td>
                    <td>
                      <code>{t.referenceId}</code>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          {result.data && <Pager page={page} size={size} total={result.data.totalElements} onPage={setPage} />}
        </div>
      )}
    </Section>
  );
}

/** Sessions tab: the player's most recent level attempts. */
function SessionsTab({ playerId }: { playerId: number }) {
  const [limit, setLimit] = useState(20);
  const result = useAsync(() => api.getSessions(playerId, limit), [playerId, limit]);
  const rows = result.data ?? [];

  return (
    <Section
      title="Recent sessions"
      actions={
        <select value={limit} onChange={(e) => setLimit(Number(e.target.value))}>
          {[20, 50, 100].map((n) => (
            <option key={n} value={n}>
              last {n}
            </option>
          ))}
        </select>
      }
    >
      <ErrorBox error={result.error} />
      {result.loading && !result.data ? (
        <Loading />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Started</th>
                <th className="num">Level</th>
                <th>Status</th>
                <th className="num">Score</th>
                <th className="num">Moves</th>
                <th className="num">Stars</th>
                <th className="num">Reward coins</th>
                <th>Strategy</th>
                <th>Completed</th>
                <th>Session</th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 ? (
                <EmptyRow colSpan={10} text="No sessions" />
              ) : (
                rows.map((s) => (
                  <tr key={s.id}>
                    <td>{formatDateTime(s.startedAt)}</td>
                    <td className="num">{s.level}</td>
                    <td>
                      <StatusBadge status={s.status} />
                    </td>
                    <td className="num">{formatNumber(s.score)}</td>
                    <td className="num">{formatNumber(s.movesUsed)}</td>
                    <td className="num">{formatNumber(s.stars)}</td>
                    <td className="num">{formatNumber(s.rewardCoins)}</td>
                    <td>{s.rewardStrategy ?? '—'}</td>
                    <td>{formatDateTime(s.completedAt)}</td>
                    <td>
                      <code title={s.id}>{s.id.slice(0, 8)}</code> <span className="muted">seed {s.seed}</span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}
    </Section>
  );
}

/** Progress tab: best result per level. */
function ProgressTab({ playerId }: { playerId: number }) {
  const result = useAsync(() => api.getProgress(playerId), [playerId]);
  const progress = result.data;

  return (
    <Section
      title="Progress"
      actions={
        progress ? (
          <span className="muted">
            current level {progress.currentLevel} · {progress.totalStars} stars total
          </span>
        ) : null
      }
    >
      <ErrorBox error={result.error} />
      {result.loading && !progress ? (
        <Loading />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th className="num">Level</th>
                <th className="num">Stars</th>
                <th className="num">Best score</th>
                <th className="num">Attempts</th>
                <th>Cleared</th>
                <th>First cleared at</th>
              </tr>
            </thead>
            <tbody>
              {!progress || progress.levels.length === 0 ? (
                <EmptyRow colSpan={6} text="No level played yet" />
              ) : (
                progress.levels.map((l) => (
                  <tr key={l.level}>
                    <td className="num">{l.level}</td>
                    <td className="num">{'★'.repeat(l.stars) || '—'}</td>
                    <td className="num">{formatNumber(l.bestScore)}</td>
                    <td className="num">{l.attempts}</td>
                    <td>{l.cleared ? 'yes' : 'no'}</td>
                    <td>{formatDateTime(l.completedAt)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}
    </Section>
  );
}

/** Event timeline tab: telemetry documents from Elasticsearch, filterable by type and time window. */
function EventsTab({ playerId }: { playerId: number }) {
  const [type, setType] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [applied, setApplied] = useState({ type: '', from: '', to: '' });
  const [page, setPage] = useState(0);
  const size = 50;
  const result = useAsync(
    () =>
      api.getPlayerEvents(playerId, {
        type: applied.type || undefined,
        from: localInputToIso(applied.from) ?? undefined,
        to: localInputToIso(applied.to) ?? undefined,
        page,
        size,
      }),
    [playerId, applied, page],
  );
  const rows = result.data?.events ?? [];

  function apply(event: FormEvent) {
    event.preventDefault();
    setPage(0);
    setApplied({ type, from, to });
  }

  return (
    <Section title={`Event timeline${result.data ? ` (${result.data.total})` : ''}`}>
      <form className="form-row" onSubmit={apply} style={{ marginBottom: 12 }}>
        <div>
          <label htmlFor="ev-type">Event type</label>
          <select id="ev-type" value={type} onChange={(e) => setType(e.target.value)}>
            <option value="">All types</option>
            {TELEMETRY_EVENT_TYPES.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="ev-from">From</label>
          <input id="ev-from" type="datetime-local" value={from} onChange={(e) => setFrom(e.target.value)} />
        </div>
        <div>
          <label htmlFor="ev-to">To</label>
          <input id="ev-to" type="datetime-local" value={to} onChange={(e) => setTo(e.target.value)} />
        </div>
        <button type="submit" className="btn-primary">
          Apply
        </button>
      </form>
      <ErrorBox error={result.error} />
      {result.loading && !result.data ? (
        <Loading />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Timestamp</th>
                <th>Event type</th>
                <th>Aggregate</th>
                <th>Payload</th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 ? (
                <EmptyRow colSpan={4} text="No events (telemetry is published asynchronously through the outbox)" />
              ) : (
                rows.map((e) => (
                  <tr key={e.id}>
                    <td style={{ whiteSpace: 'nowrap' }}>{formatDateTime(e.timestamp)}</td>
                    <td>{e.eventType}</td>
                    <td>
                      <code>
                        {e.aggregateType ?? '?'}/{e.aggregateId ?? '?'}
                      </code>
                    </td>
                    <td>
                      <JsonCode value={e.payload ?? {}} />
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          {result.data && <Pager page={page} size={size} total={result.data.total} onPage={setPage} />}
        </div>
      )}
    </Section>
  );
}
