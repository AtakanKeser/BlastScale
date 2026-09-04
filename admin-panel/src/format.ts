// Small formatting helpers shared by the pages: dates, numbers, durations, JSON, datetime-local inputs.

/** ISO-8601 instant in the browser's local time zone; "—" when absent. */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleString(undefined, {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
}

/** Thousands-separated number with at most `maximumFractionDigits` decimals; "n/a" when missing. */
export function formatNumber(value: number | null | undefined, maximumFractionDigits = 0): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return 'n/a';
  return value.toLocaleString(undefined, { maximumFractionDigits });
}

/** Milliseconds with unit, e.g. "12.3 ms"; "n/a" when missing. */
export function formatMillis(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return 'n/a';
  return `${formatNumber(value, 1)} ms`;
}

/** Ratio in 0..1 as a percentage, e.g. 0.0123 -> "1.23%"; "n/a" when missing. */
export function formatPercent(ratio: number | null | undefined, digits = 2): string {
  if (ratio === null || ratio === undefined || !Number.isFinite(ratio)) return 'n/a';
  return `${(ratio * 100).toFixed(digits)}%`;
}

/** Seconds -> "2d 3h 4m 5s" style duration; "n/a" when missing. */
export function formatDuration(totalSeconds: number | null | undefined): string {
  if (totalSeconds === null || totalSeconds === undefined || !Number.isFinite(totalSeconds)) return 'n/a';
  const s = Math.max(0, Math.floor(totalSeconds));
  const days = Math.floor(s / 86400);
  const hours = Math.floor((s % 86400) / 3600);
  const minutes = Math.floor((s % 3600) / 60);
  const seconds = s % 60;
  const parts: string[] = [];
  if (days) parts.push(`${days}d`);
  if (days || hours) parts.push(`${hours}h`);
  if (days || hours || minutes) parts.push(`${minutes}m`);
  parts.push(`${seconds}s`);
  return parts.join(' ');
}

/** Single-line JSON for table cells. */
export function compactJson(value: unknown): string {
  if (value === undefined) return '';
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}

/** Indented JSON for textareas. */
export function prettyJson(value: unknown): string {
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

/** Value of an <input type="datetime-local"> (local time) -> ISO instant in UTC; null for empty/invalid. */
export function localInputToIso(value: string): string | null {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

/** Date -> value accepted by <input type="datetime-local"> (local time, minute precision). */
export function toDatetimeLocal(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** Sum of a {name: count} map; null when the map is missing. */
export function sumValues(map: Record<string, number> | null | undefined): number | null {
  if (!map) return null;
  return Object.values(map).reduce((total, value) => total + value, 0);
}

/** "{name: count}" map as "a: 1 · b: 2" text. */
export function entriesText(map: Record<string, number> | null | undefined, digits = 0): string {
  if (!map) return '';
  return Object.entries(map)
    .map(([key, value]) => `${key}: ${formatNumber(value, digits)}`)
    .join(' · ');
}
