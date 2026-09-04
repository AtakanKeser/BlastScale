import { useState, type FormEvent } from 'react';
import * as api from '../api';
import { useAsync } from '../hooks/useAsync';
import { useToast } from '../components/Toast';
import { EmptyRow, ErrorBox, JsonCode, Loading, Section } from '../components/ui';
import { formatDateTime, formatNumber, prettyJson } from '../format';
import type { LevelDefinition, UpsertLevelRequest } from '../types';

/** Starting values for a level that does not exist in MongoDB yet. */
const DEFAULT_LEVEL: UpsertLevelRequest = {
  rows: 8,
  cols: 8,
  colorCount: 5,
  moveLimit: 25,
  targetScore: 1000,
  starThresholds: [1000, 1500, 2000],
  specialRules: {},
};

/** Level definitions table (default range 1..50) with an edit form for the selected level. */
export default function LevelsPage() {
  const [range, setRange] = useState({ from: 1, to: 50 });
  const [fromDraft, setFromDraft] = useState('1');
  const [toDraft, setToDraft] = useState('50');
  const [selected, setSelected] = useState<number | null>(null);
  const [editDraft, setEditDraft] = useState('');
  const levels = useAsync(() => api.listLevels(range.from, range.to), [range]);

  function applyRange(event: FormEvent) {
    event.preventDefault();
    const from = Number(fromDraft);
    const to = Number(toDraft);
    if (!Number.isInteger(from) || !Number.isInteger(to) || from < 1 || to < from) return;
    setRange({ from, to: Math.min(to, from + 200) });
  }

  function editByNumber(event: FormEvent) {
    event.preventDefault();
    const n = Number(editDraft);
    if (Number.isInteger(n) && n >= 1) setSelected(n);
  }

  const rows = levels.data ?? [];
  const selectedDefinition = selected === null ? null : (rows.find((l) => l.levelNumber === selected) ?? null);

  return (
    <div>
      <div className="page-header">
        <h1>Levels</h1>
        <div className="form-row">
          <form className="form-row" onSubmit={applyRange}>
            <div>
              <label htmlFor="lv-from">From</label>
              <input id="lv-from" type="number" min={1} value={fromDraft} onChange={(e) => setFromDraft(e.target.value)} style={{ width: 80 }} />
            </div>
            <div>
              <label htmlFor="lv-to">To</label>
              <input id="lv-to" type="number" min={1} value={toDraft} onChange={(e) => setToDraft(e.target.value)} style={{ width: 80 }} />
            </div>
            <button type="submit">Load</button>
          </form>
          <form className="form-row" onSubmit={editByNumber}>
            <div>
              <label htmlFor="lv-edit">Edit level #</label>
              <input id="lv-edit" type="number" min={1} value={editDraft} onChange={(e) => setEditDraft(e.target.value)} style={{ width: 80 }} />
            </div>
            <button type="submit">Edit</button>
          </form>
        </div>
      </div>

      {selected !== null && (
        <LevelEditor
          key={selected}
          levelNumber={selected}
          existing={selectedDefinition}
          onSaved={() => levels.reload()}
          onClose={() => setSelected(null)}
        />
      )}

      <Section title={`Levels ${range.from}–${range.to} (${rows.length} defined)`}>
        <p className="muted">
          Levels are generated procedurally on first play and written to MongoDB; numbers missing here have not been
          played or hand-tuned yet. Saving a level bumps its version, marks it as source "admin" and evicts the cache.
        </p>
        <ErrorBox error={levels.error} />
        {levels.loading && !levels.data ? (
          <Loading />
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th className="num">Level</th>
                  <th className="num">Version</th>
                  <th>Board</th>
                  <th className="num">Colors</th>
                  <th className="num">Moves</th>
                  <th className="num">Target</th>
                  <th>Star thresholds</th>
                  <th>Special rules</th>
                  <th>Source</th>
                  <th>Updated</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 ? (
                  <EmptyRow colSpan={11} text="No level definitions in this range" />
                ) : (
                  rows.map((l) => (
                    <tr key={l.levelNumber} className="clickable" onClick={() => setSelected(l.levelNumber)}>
                      <td className="num">{l.levelNumber}</td>
                      <td className="num">{l.version}</td>
                      <td>
                        {l.rows} × {l.cols}
                      </td>
                      <td className="num">{l.colorCount}</td>
                      <td className="num">{l.moveLimit}</td>
                      <td className="num">{formatNumber(l.targetScore)}</td>
                      <td>{l.starThresholds.map((t) => formatNumber(t)).join(' / ')}</td>
                      <td>{l.specialRules && Object.keys(l.specialRules).length > 0 ? <JsonCode value={l.specialRules} /> : <span className="muted">—</span>}</td>
                      <td>{l.source ?? '—'}</td>
                      <td>{formatDateTime(l.updatedAt)}</td>
                      <td>
                        <button type="button" className="btn-small">
                          Edit
                        </button>
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

/** Parses a non-negative integer from an input; NaN when the text is not an integer. */
function parseInt10(text: string): number {
  return /^-?\d+$/.test(text.trim()) ? Number(text.trim()) : Number.NaN;
}

/** Edit form for one level (PUT /admin/levels/{n}); validation mirrors UpsertLevelRequest and LevelDefinitionService. */
function LevelEditor({
  levelNumber,
  existing,
  onSaved,
  onClose,
}: {
  levelNumber: number;
  existing: LevelDefinition | null;
  onSaved: (saved: LevelDefinition) => void;
  onClose: () => void;
}) {
  const toast = useToast();
  const base: UpsertLevelRequest = existing
    ? {
        rows: existing.rows,
        cols: existing.cols,
        colorCount: existing.colorCount,
        moveLimit: existing.moveLimit,
        targetScore: existing.targetScore,
        starThresholds: existing.starThresholds,
        specialRules: existing.specialRules ?? {},
      }
    : DEFAULT_LEVEL;
  const [rows, setRows] = useState(String(base.rows));
  const [cols, setCols] = useState(String(base.cols));
  const [colorCount, setColorCount] = useState(String(base.colorCount));
  const [moveLimit, setMoveLimit] = useState(String(base.moveLimit));
  const [targetScore, setTargetScore] = useState(String(base.targetScore));
  const [thresholds, setThresholds] = useState<string[]>(() => {
    const values = base.starThresholds.map(String);
    while (values.length < 3) values.push('');
    return values.slice(0, 3);
  });
  const [rules, setRules] = useState(() => prettyJson(base.specialRules ?? {}));
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  /** Returns the request body, or an error message. */
  function validate(): UpsertLevelRequest | string {
    const r = parseInt10(rows);
    const c = parseInt10(cols);
    const colors = parseInt10(colorCount);
    const moves = parseInt10(moveLimit);
    const target = parseInt10(targetScore);
    if (!(r >= 4 && r <= 12)) return 'rows must be between 4 and 12';
    if (!(c >= 4 && c <= 12)) return 'cols must be between 4 and 12';
    if (!(colors >= 3 && colors <= 8)) return 'colorCount must be between 3 and 8';
    if (!(moves >= 5 && moves <= 60)) return 'moveLimit must be between 5 and 60';
    if (!(target >= 100)) return 'targetScore must be at least 100';
    const th = thresholds.map(parseInt10);
    if (th.some((v) => Number.isNaN(v))) return 'starThresholds need 3 integers';
    if (th[0] !== target) return 'starThresholds[0] must equal targetScore (1 star = reaching the target)';
    if (!(th[0] <= th[1] && th[1] <= th[2])) return 'starThresholds must be ascending';
    let specialRules: Record<string, unknown>;
    try {
      const parsed: unknown = JSON.parse(rules.trim() || '{}');
      if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return 'specialRules must be a JSON object';
      specialRules = parsed as Record<string, unknown>;
    } catch {
      return 'specialRules must be valid JSON';
    }
    return { rows: r, cols: c, colorCount: colors, moveLimit: moves, targetScore: target, starThresholds: th, specialRules };
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    const body = validate();
    if (typeof body === 'string') {
      setError(body);
      return;
    }
    setError(null);
    setBusy(true);
    try {
      const saved = await api.upsertLevel(levelNumber, body);
      toast.success(`Saved level ${saved.levelNumber} (version ${saved.version})`);
      onSaved(saved);
    } catch (err) {
      toast.error(err);
    } finally {
      setBusy(false);
    }
  }

  function updateThreshold(index: number, value: string) {
    setThresholds((current) => current.map((t, i) => (i === index ? value : t)));
  }

  return (
    <Section
      title={`Edit level ${levelNumber}${existing ? ` (version ${existing.version}, source ${existing.source ?? '?'})` : ' (new definition)'}`}
      actions={
        <button type="button" onClick={onClose}>
          Close
        </button>
      }
    >
      <form onSubmit={submit} className="form-grid">
        <div>
          <label htmlFor="lv-rows">Rows (4–12)</label>
          <input id="lv-rows" type="number" min={4} max={12} value={rows} onChange={(e) => setRows(e.target.value)} />
        </div>
        <div>
          <label htmlFor="lv-cols">Cols (4–12)</label>
          <input id="lv-cols" type="number" min={4} max={12} value={cols} onChange={(e) => setCols(e.target.value)} />
        </div>
        <div>
          <label htmlFor="lv-colors">Colors (3–8)</label>
          <input id="lv-colors" type="number" min={3} max={8} value={colorCount} onChange={(e) => setColorCount(e.target.value)} />
        </div>
        <div>
          <label htmlFor="lv-moves">Move limit (5–60)</label>
          <input id="lv-moves" type="number" min={5} max={60} value={moveLimit} onChange={(e) => setMoveLimit(e.target.value)} />
        </div>
        <div>
          <label htmlFor="lv-target">Target score (≥ 100)</label>
          <input id="lv-target" type="number" min={100} value={targetScore} onChange={(e) => setTargetScore(e.target.value)} />
        </div>
        <div>
          <label>Star thresholds (1★ = target, 2★, 3★)</label>
          <div className="form-row">
            {thresholds.map((t, index) => (
              <input
                key={index}
                type="number"
                min={100}
                value={t}
                onChange={(e) => updateThreshold(index, e.target.value)}
                style={{ width: 72 }}
                aria-label={`${index + 1} star threshold`}
              />
            ))}
          </div>
        </div>
        <div className="full">
          <label htmlFor="lv-rules">Special rules JSON (free-form, passed to the client as-is)</label>
          <textarea id="lv-rules" value={rules} onChange={(e) => setRules(e.target.value)} spellCheck={false} style={{ minHeight: 80 }} />
        </div>
        <div className="full form-footer">
          <button type="submit" className="btn-primary" disabled={busy}>
            {busy ? 'Saving…' : 'Save level'}
          </button>
          {error && <span className="error-box" style={{ margin: 0 }}>{error}</span>}
        </div>
      </form>
    </Section>
  );
}
