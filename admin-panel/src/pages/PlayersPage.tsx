import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import * as api from '../api';
import { useAsync } from '../hooks/useAsync';
import { EmptyRow, ErrorBox, Loading, Pager, Section, StatusBadge } from '../components/ui';
import { formatDateTime } from '../format';

const PAGE_SIZE = 20;

/** Player search (username substring) with paging; clicking a row opens the player detail page. */
export default function PlayersPage() {
  const navigate = useNavigate();
  const [draft, setDraft] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(0);
  const [idDraft, setIdDraft] = useState('');
  const result = useAsync(() => api.searchPlayers(query, page, PAGE_SIZE), [query, page]);

  function search(event: FormEvent) {
    event.preventDefault();
    setPage(0);
    setQuery(draft.trim());
  }

  function openById(event: FormEvent) {
    event.preventDefault();
    const id = Number(idDraft);
    if (Number.isInteger(id) && id > 0) navigate(`/players/${id}`);
  }

  const rows = result.data?.players ?? [];

  return (
    <div>
      <div className="page-header">
        <h1>Players</h1>
      </div>

      <Section title="Search">
        <div className="form-row">
          <form className="form-row" onSubmit={search}>
            <div>
              <label htmlFor="q">Username contains</label>
              <input id="q" value={draft} onChange={(e) => setDraft(e.target.value)} placeholder="e.g. guest_" />
            </div>
            <button type="submit" className="btn-primary">
              Search
            </button>
          </form>
          <form className="form-row" onSubmit={openById}>
            <div>
              <label htmlFor="pid">Open by id</label>
              <input
                id="pid"
                value={idDraft}
                onChange={(e) => setIdDraft(e.target.value)}
                inputMode="numeric"
                placeholder="123"
                style={{ width: 110 }}
              />
            </div>
            <button type="submit">Open</button>
          </form>
        </div>
      </Section>

      <Section title={`Results${result.data ? ` (${result.data.total})` : ''}`}>
        <ErrorBox error={result.error} />
        {result.loading && !result.data ? (
          <Loading />
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Id</th>
                  <th>Username</th>
                  <th>Role</th>
                  <th className="num">Level</th>
                  <th>Guest</th>
                  <th>Created</th>
                  <th>Last seen</th>
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 ? (
                  <EmptyRow colSpan={7} text="No players match" />
                ) : (
                  rows.map((p) => (
                    <tr key={p.id} className="clickable" onClick={() => navigate(`/players/${p.id}`)}>
                      <td>{p.id}</td>
                      <td>{p.username}</td>
                      <td>
                        <StatusBadge status={p.role} />
                      </td>
                      <td className="num">{p.currentLevel}</td>
                      <td>{p.guest ? 'yes' : 'no'}</td>
                      <td>{formatDateTime(p.createdAt)}</td>
                      <td>{formatDateTime(p.lastSeenAt)}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
            {result.data && <Pager page={page} size={PAGE_SIZE} total={result.data.total} onPage={setPage} />}
          </div>
        )}
      </Section>
    </div>
  );
}
