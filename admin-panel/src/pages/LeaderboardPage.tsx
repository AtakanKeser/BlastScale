import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import * as api from '../api';
import { useAsync } from '../hooks/useAsync';
import { useToast } from '../components/Toast';
import { EmptyRow, ErrorBox, Loading, Section, StatusBadge } from '../components/ui';
import { formatDateTime, formatNumber } from '../format';
import type { FinalizationResult } from '../types';

const SEASON_PATTERN = /^\d{4}-W\d{2}$/;

/** Weekly leaderboard: current season (or any season id like 2026-W36) and a forced finalization trigger. */
export default function LeaderboardPage() {
  const toast = useToast();
  const [seasonDraft, setSeasonDraft] = useState('');
  /** null = the current season (GET /leaderboards/current) */
  const [season, setSeason] = useState<string | null>(null);
  const board = useAsync(() => (season ? api.getLeaderboardSeason(season, 100) : api.getCurrentLeaderboard(100)), [season]);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<FinalizationResult | null>(null);

  function lookup(event: FormEvent) {
    event.preventDefault();
    const value = seasonDraft.trim().toUpperCase();
    if (!value) {
      setSeason(null);
      return;
    }
    if (!SEASON_PATTERN.test(value)) {
      toast.error('Season id must look like 2026-W36');
      return;
    }
    setSeason(value);
  }

  async function finalize() {
    const target = board.data?.season;
    if (!target) return;
    if (
      !window.confirm(
        `Finalize season ${target} now with force=true? Prizes are paid to the top ranks immediately. ` +
          'Re-running for an already finalized season is a no-op.',
      )
    ) {
      return;
    }
    setBusy(true);
    try {
      const r = await api.finalizeSeason(target, true);
      setResult(r);
      toast.success(
        r.alreadyFinalized
          ? `Season ${r.season} was already finalized on ${formatDateTime(r.finalizedAt)}`
          : `Season ${r.season} finalized: ${r.rewards.length} players rewarded`,
      );
      board.reload();
    } catch (err) {
      toast.error(err);
    } finally {
      setBusy(false);
    }
  }

  const view = board.data;

  return (
    <div>
      <div className="page-header">
        <h1>Leaderboard</h1>
        <form className="form-row" onSubmit={lookup}>
          <div>
            <label htmlFor="season">Season id (empty = current)</label>
            <input
              id="season"
              value={seasonDraft}
              onChange={(e) => setSeasonDraft(e.target.value)}
              placeholder="2026-W36"
              style={{ width: 140 }}
            />
          </div>
          <button type="submit">Load</button>
          <button type="button" onClick={board.reload}>
            Refresh
          </button>
        </form>
      </div>

      <Section
        title={view ? `Season ${view.season}` : 'Season'}
        actions={
          <>
            {view && <StatusBadge status={view.finalized ? 'FINALIZED' : 'ACTIVE'} />}
            <button type="button" className="btn-danger" disabled={busy || !view || view.finalized} onClick={() => void finalize()}>
              {busy ? 'Finalizing…' : 'Finalize season (force)'}
            </button>
          </>
        }
      >
        <ErrorBox error={board.error} />
        {board.loading && !view ? (
          <Loading />
        ) : view ? (
          <>
            <dl className="props" style={{ marginBottom: 12 }}>
              <dt>Season</dt>
              <dd>{view.season}</dd>
              <dt>Ends at</dt>
              <dd>{formatDateTime(view.endsAt)}</dd>
              <dt>Finalized</dt>
              <dd>{view.finalized ? 'yes (prizes paid)' : 'no'}</dd>
              <dt>Players shown</dt>
              <dd>{view.players.length} (top 100)</dd>
            </dl>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th className="num">Rank</th>
                    <th>Player</th>
                    <th>Id</th>
                    <th className="num">Score</th>
                  </tr>
                </thead>
                <tbody>
                  {view.players.length === 0 ? (
                    <EmptyRow colSpan={4} text="No scores recorded for this season" />
                  ) : (
                    view.players.map((p) => (
                      <tr key={p.playerId}>
                        <td className="num">{p.rank}</td>
                        <td>
                          <Link to={`/players/${p.playerId}`}>{p.name}</Link>
                        </td>
                        <td>{p.playerId}</td>
                        <td className="num">{formatNumber(p.score)}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </>
        ) : null}
      </Section>

      {result && (
        <Section title={`Finalization result: ${result.season}`} actions={<button type="button" onClick={() => setResult(null)}>Dismiss</button>}>
          <dl className="props" style={{ marginBottom: 12 }}>
            <dt>Already finalized</dt>
            <dd>{result.alreadyFinalized ? 'yes' : 'no'}</dd>
            <dt>Finalized at</dt>
            <dd>{formatDateTime(result.finalizedAt)}</dd>
            <dt>Participants</dt>
            <dd>{formatNumber(result.participants)}</dd>
          </dl>
          <table>
            <thead>
              <tr>
                <th className="num">Rank</th>
                <th>Player id</th>
                <th className="num">Score</th>
                <th className="num">Coins</th>
              </tr>
            </thead>
            <tbody>
              {result.rewards.length === 0 ? (
                <EmptyRow colSpan={4} text="Nobody rewarded" />
              ) : (
                result.rewards.map((r) => (
                  <tr key={r.playerId}>
                    <td className="num">{r.rank}</td>
                    <td>
                      <Link to={`/players/${r.playerId}`}>{r.playerId}</Link>
                    </td>
                    <td className="num">{formatNumber(r.score)}</td>
                    <td className="num">{formatNumber(r.coins)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </Section>
      )}
    </div>
  );
}
