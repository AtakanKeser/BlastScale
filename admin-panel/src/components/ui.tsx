import type { ReactNode } from 'react';
import { errorText } from '../api';
import { compactJson } from '../format';

/** Maps a status-like string (event, experiment, session, health, ledger type) to a badge colour. */
function toneOf(status: string): 'green' | 'blue' | 'yellow' | 'red' | 'gray' {
  switch (status.toUpperCase()) {
    case 'ACTIVE':
    case 'RUNNING':
    case 'UP':
    case 'COMPLETED':
    case 'FINALIZED':
    case 'CREDIT':
      return 'green';
    case 'SCHEDULED':
    case 'ENDED':
    case 'ADMIN':
      return 'blue';
    case 'DRAFT':
    case 'PAUSED':
    case 'UNKNOWN':
    case 'OUT_OF_SERVICE':
    case 'ABANDONED':
    case 'EXPIRED':
      return 'yellow';
    case 'CANCELLED':
    case 'FAILED':
    case 'DOWN':
    case 'DEBIT':
      return 'red';
    default:
      return 'gray';
  }
}

/** Coloured pill for a status string. */
export function StatusBadge({ status }: { status: string | null | undefined }) {
  const text = status ?? 'UNKNOWN';
  return <span className={`badge badge-${toneOf(text)}`}>{text}</span>;
}

/** Inline error panel; renders nothing when there is no error. */
export function ErrorBox({ error }: { error: unknown }) {
  if (!error) return null;
  return <div className="error-box">{errorText(error)}</div>;
}

/** Placeholder shown while the first load of a section is in flight. */
export function Loading({ text = 'Loading…' }: { text?: string }) {
  return <div className="muted">{text}</div>;
}

/** Dashboard-style stat card: small label, big value, optional hint line. */
export function StatCard({ label, value, hint }: { label: string; value: ReactNode; hint?: ReactNode }) {
  return (
    <div className="card">
      <div className="card-label">{label}</div>
      <div className="card-value">{value}</div>
      {hint ? <div className="card-hint">{hint}</div> : null}
    </div>
  );
}

/** White panel with a title bar and optional action buttons on the right. */
export function Section({ title, actions, children }: { title: ReactNode; actions?: ReactNode; children: ReactNode }) {
  return (
    <section className="section">
      <div className="section-header">
        <h2>{title}</h2>
        {actions ? <div className="actions">{actions}</div> : null}
      </div>
      <div className="section-body">{children}</div>
    </section>
  );
}

/** Compact single-line JSON, wrapped so long payloads do not break the table layout. */
export function JsonCode({ value }: { value: unknown }) {
  return <code className="json">{compactJson(value)}</code>;
}

/** Prev/next paging controls for zero-based pages. */
export function Pager({ page, size, total, onPage }: { page: number; size: number; total: number; onPage: (page: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / Math.max(1, size)));
  return (
    <div className="pager">
      <button type="button" disabled={page <= 0} onClick={() => onPage(page - 1)}>
        Prev
      </button>
      <span className="muted">
        Page {page + 1} of {pages} · {total} total
      </span>
      <button type="button" disabled={page + 1 >= pages} onClick={() => onPage(page + 1)}>
        Next
      </button>
    </div>
  );
}

/** Single full-width table row used when a list is empty. */
export function EmptyRow({ colSpan, text = 'Nothing to show' }: { colSpan: number; text?: string }) {
  return (
    <tr>
      <td colSpan={colSpan} className="muted">
        {text}
      </td>
    </tr>
  );
}
