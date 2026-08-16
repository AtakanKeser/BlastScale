import { useState, type FormEvent } from 'react';
import * as api from '../api';
import { useAsync } from '../hooks/useAsync';
import { useToast } from '../components/Toast';
import { EmptyRow, ErrorBox, JsonCode, Loading, Section } from '../components/ui';
import { formatDateTime, prettyJson } from '../format';
import { KNOWN_CONFIG_KEYS, type ConfigEntryView } from '../types';

/** Parses a textarea into a JSON value; null is rejected because the backend requires a value. */
function parseJsonValue(text: string): { value?: unknown; error?: string } {
  try {
    const value: unknown = JSON.parse(text);
    if (value === null) return { error: 'Value cannot be null' };
    return { value };
  } catch {
    return { error: 'Value must be valid JSON, e.g. 5, true, "text" or {"HAMMER": 100}' };
  }
}

/** Remote config table with inline editing (PUT per key) and a small form to add a new key. */
export default function ConfigPage() {
  const toast = useToast();
  const entries = useAsync(() => api.listConfig(), []);
  const [editingKey, setEditingKey] = useState<string | null>(null);
  const [valueText, setValueText] = useState('');
  const [descriptionText, setDescriptionText] = useState('');
  const [editError, setEditError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function startEdit(entry: ConfigEntryView) {
    setEditingKey(entry.key);
    setValueText(prettyJson(entry.value));
    setDescriptionText(entry.description ?? '');
    setEditError(null);
  }

  function cancelEdit() {
    setEditingKey(null);
    setEditError(null);
  }

  async function save(key: string) {
    const parsed = parseJsonValue(valueText);
    if (parsed.error) {
      setEditError(parsed.error);
      return;
    }
    setBusy(true);
    try {
      await api.updateConfig(key, { value: parsed.value, description: descriptionText.trim() || undefined });
      toast.success(`Saved ${key}; players receive it within 60s`);
      setEditingKey(null);
      entries.reload();
    } catch (err) {
      toast.error(err);
    } finally {
      setBusy(false);
    }
  }

  const rows = entries.data ?? [];
  const existingKeys = new Set(rows.map((r) => r.key));

  return (
    <div>
      <div className="page-header">
        <h1>Remote config</h1>
        <button type="button" onClick={entries.reload}>
          Refresh
        </button>
      </div>

      <div className="hint">
        Changes reach players within 60 seconds (Redis cache TTL) without a client update. Experiments may override
        any key per player on top of these base values.
      </div>

      <Section title={`Keys (${rows.length})`}>
        <ErrorBox error={entries.error} />
        {entries.loading && !entries.data ? (
          <Loading />
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Key</th>
                  <th style={{ width: '32%' }}>Value (JSON)</th>
                  <th>Description</th>
                  <th>Updated</th>
                  <th>By</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 ? (
                  <EmptyRow colSpan={6} text="No config keys (the Flyway seed should have created them)" />
                ) : (
                  rows.map((entry) =>
                    editingKey === entry.key ? (
                      <tr key={entry.key}>
                        <td>
                          <code>{entry.key}</code>
                        </td>
                        <td>
                          <textarea value={valueText} onChange={(e) => setValueText(e.target.value)} spellCheck={false} />
                          {editError && <div className="error-box" style={{ marginTop: 6 }}>{editError}</div>}
                        </td>
                        <td>
                          <input
                            value={descriptionText}
                            maxLength={255}
                            onChange={(e) => setDescriptionText(e.target.value)}
                            style={{ width: '100%' }}
                          />
                        </td>
                        <td>{formatDateTime(entry.updatedAt)}</td>
                        <td>{entry.updatedBy ?? '—'}</td>
                        <td>
                          <div className="actions">
                            <button type="button" className="btn-small btn-primary" disabled={busy} onClick={() => void save(entry.key)}>
                              Save
                            </button>
                            <button type="button" className="btn-small" disabled={busy} onClick={cancelEdit}>
                              Cancel
                            </button>
                          </div>
                        </td>
                      </tr>
                    ) : (
                      <tr key={entry.key}>
                        <td>
                          <code>{entry.key}</code>
                        </td>
                        <td>
                          <JsonCode value={entry.value} />
                        </td>
                        <td>{entry.description ?? <span className="muted">—</span>}</td>
                        <td>{formatDateTime(entry.updatedAt)}</td>
                        <td>{entry.updatedBy ?? '—'}</td>
                        <td>
                          <button type="button" className="btn-small" disabled={editingKey !== null} onClick={() => startEdit(entry)}>
                            Edit
                          </button>
                        </td>
                      </tr>
                    ),
                  )
                )}
              </tbody>
            </table>
          </div>
        )}
      </Section>

      <Section title="Add key">
        <AddKeyForm existingKeys={existingKeys} onSaved={entries.reload} />
      </Section>
    </div>
  );
}

/** Creates a key that is not in the table yet (PUT is an upsert); suggests the well-known keys. */
function AddKeyForm({ existingKeys, onSaved }: { existingKeys: Set<string>; onSaved: () => void }) {
  const toast = useToast();
  const [key, setKey] = useState('');
  const [valueText, setValueText] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const suggestions = KNOWN_CONFIG_KEYS.filter((k) => !existingKeys.has(k));

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    const trimmedKey = key.trim();
    if (!trimmedKey || trimmedKey.length > 64) {
      setError('Key is required (max 64 characters)');
      return;
    }
    const parsed = parseJsonValue(valueText);
    if (parsed.error) {
      setError(parsed.error);
      return;
    }
    setBusy(true);
    try {
      await api.updateConfig(trimmedKey, { value: parsed.value, description: description.trim() || undefined });
      toast.success(`Saved ${trimmedKey}`);
      setKey('');
      setValueText('');
      setDescription('');
      onSaved();
    } catch (err) {
      toast.error(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit} className="form-grid">
      <div>
        <label htmlFor="cfg-key">Key</label>
        <input id="cfg-key" list="cfg-key-suggestions" value={key} onChange={(e) => setKey(e.target.value)} required style={{ width: '100%' }} />
        <datalist id="cfg-key-suggestions">
          {suggestions.map((k) => (
            <option key={k} value={k} />
          ))}
        </datalist>
      </div>
      <div>
        <label htmlFor="cfg-value">Value (JSON)</label>
        <input id="cfg-value" value={valueText} onChange={(e) => setValueText(e.target.value)} placeholder='e.g. 5, true, "text", {"a": 1}' required style={{ width: '100%' }} />
      </div>
      <div>
        <label htmlFor="cfg-desc">Description (optional)</label>
        <input id="cfg-desc" value={description} maxLength={255} onChange={(e) => setDescription(e.target.value)} style={{ width: '100%' }} />
      </div>
      <div className="full form-footer">
        <button type="submit" className="btn-primary" disabled={busy}>
          {busy ? 'Saving…' : 'Save key'}
        </button>
        {error && <span className="error-box" style={{ margin: 0 }}>{error}</span>}
      </div>
    </form>
  );
}
