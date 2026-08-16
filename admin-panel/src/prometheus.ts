// Minimal Prometheus HTTP API client used by the dashboard header cards.
import type { PromQueryResponse, PromResult } from './types';

/** Prometheus base URL (its HTTP API allows cross-origin reads by default). */
export const PROMETHEUS_URL = (import.meta.env.VITE_PROMETHEUS_URL || 'http://localhost:9090').replace(/\/+$/, '');

/** PromQL for the dashboard. Metric names come from Micrometer (http_server_requests_*) and the backend's GameplayMetrics. */
export const PROM_QUERIES = {
  requestsPerSecond: 'sum(rate(http_server_requests_seconds_count{application="blastscale"}[1m]))',
  p95Seconds:
    'histogram_quantile(0.95, sum(rate(http_server_requests_seconds_bucket{application="blastscale"}[1m])) by (le))',
  p99Seconds:
    'histogram_quantile(0.99, sum(rate(http_server_requests_seconds_bucket{application="blastscale"}[1m])) by (le))',
  errorRate:
    'sum(rate(http_server_requests_seconds_count{application="blastscale",status=~"5.."}[1m])) / sum(rate(http_server_requests_seconds_count{application="blastscale"}[1m]))',
  cacheHitRate:
    'sum(rate(blastscale_cache_requests_total{result="hit"}[5m])) / sum(rate(blastscale_cache_requests_total{result=~"hit|miss"}[5m]))',
  completionsPerMinute: 'sum(rate(blastscale_level_completion_total[5m])) by (result) * 60',
} as const;

const TIMEOUT_MS = 5000;

/** Runs an instant query and returns the result vector; throws when Prometheus is unreachable or rejects the query. */
export async function instantQuery(query: string): Promise<PromResult[]> {
  const controller = new AbortController();
  const timer = window.setTimeout(() => controller.abort(), TIMEOUT_MS);
  try {
    const url = `${PROMETHEUS_URL}/api/v1/query?query=${encodeURIComponent(query)}`;
    const response = await fetch(url, { signal: controller.signal, headers: { Accept: 'application/json' } });
    const body = (await response.json()) as PromQueryResponse;
    if (!response.ok || body.status !== 'success' || !body.data) {
      throw new Error(body.error ?? `Prometheus HTTP ${response.status}`);
    }
    return body.data.result;
  } finally {
    window.clearTimeout(timer);
  }
}

/** Parses one sample; Prometheus encodes NaN/Inf as strings, which become null here. */
function sampleValue(result: PromResult | undefined): number | null {
  if (!result) return null;
  const value = Number(result.value[1]);
  return Number.isFinite(value) ? value : null;
}

/** First sample of an instant query as a number; null when the vector is empty or the value is NaN. */
export async function scalar(query: string): Promise<number | null> {
  const results = await instantQuery(query);
  return sampleValue(results[0]);
}

/** Instant vector grouped by one label, e.g. {success: 12, failed: 3}. */
export async function vectorBy(query: string, label: string): Promise<Record<string, number>> {
  const results = await instantQuery(query);
  const out: Record<string, number> = {};
  for (const result of results) {
    const value = sampleValue(result);
    if (value !== null) out[result.metric[label] ?? 'unknown'] = value;
  }
  return out;
}
