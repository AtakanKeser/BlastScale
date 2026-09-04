// Typed fetch client for the BlastScale admin API: one function per backend endpoint.
// Request/response shapes live in types.ts and mirror the Java DTO records exactly.
import { clearSession, getSession } from './auth';
import type {
  ApiErrorBody,
  AuthResponse,
  ConfigEntryView,
  CreateEventRequest,
  CreateExperimentRequest,
  Dashboard,
  EventPage,
  ExperimentView,
  FinalizationResult,
  GrantRequest,
  Health,
  LeaderboardView,
  LevelDefinition,
  LiveEventView,
  OutboxStats,
  Page,
  PagedPlayers,
  PlayerProfile,
  ProgressView,
  SessionView,
  TransactionView,
  UpdateConfigRequest,
  UpsertLevelRequest,
  WalletSnapshot,
} from './types';

/** Base URL of the backend (nginx in docker-compose, or Spring Boot directly); trailing slashes removed. */
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080').replace(/\/+$/, '');

/** Error thrown for every failed request. Carries the backend's ApiError fields (code, message, details, path). */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly details: Record<string, unknown>;
  readonly path: string | undefined;

  constructor(status: number, code: string, message: string, details: Record<string, unknown> = {}, path?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.details = details;
    this.path = path;
  }

  /** "CODE: message", the form shown in toasts and inline error boxes. */
  get display(): string {
    return `${this.code}: ${this.message}`;
  }
}

/** Human readable text for anything thrown; API errors render as "CODE: message". */
export function errorText(error: unknown): string {
  if (error instanceof ApiError) return error.display;
  if (error instanceof Error) return error.message;
  return String(error);
}

type QueryValue = string | number | boolean | null | undefined;

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  query?: Record<string, QueryValue>;
  /** false for public endpoints (login): no bearer token and no redirect on 401. */
  auth?: boolean;
}

/** Builds the absolute URL, appending only the query parameters that have a value. */
function buildUrl(path: string, query?: Record<string, QueryValue>): string {
  const url = new URL(API_BASE_URL + path, window.location.origin);
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        url.searchParams.set(key, String(value));
      }
    }
  }
  return url.toString();
}

/** Turns a failed response into an ApiError, tolerating non-JSON bodies (nginx error pages, empty bodies). */
async function toApiError(response: Response): Promise<ApiError> {
  const text = await response.text().catch(() => '');
  try {
    const body = JSON.parse(text) as Partial<ApiErrorBody>;
    if (body && typeof body.code === 'string') {
      return new ApiError(response.status, body.code, body.message ?? response.statusText, body.details ?? {}, body.path);
    }
  } catch {
    // not JSON: fall through to the generic error below
  }
  return new ApiError(response.status, `HTTP_${response.status}`, text.slice(0, 200) || response.statusText || 'Request failed');
}

/** The token was rejected or is gone: forget it and go to the login page. */
function handleUnauthorized(): void {
  clearSession();
  if (window.location.pathname !== '/login') {
    window.location.assign('/login');
  }
}

/** Core wrapper: JSON in/out, bearer token, uniform error translation, 401 -> login. */
async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';
  if (options.auth !== false) {
    const session = getSession();
    if (session) headers.Authorization = `Bearer ${session.token}`;
  }

  let response: Response;
  try {
    response = await fetch(buildUrl(path, options.query), {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    });
  } catch {
    throw new ApiError(0, 'NETWORK_ERROR', `Cannot reach the API at ${API_BASE_URL}`);
  }

  if (response.status === 401 && options.auth !== false) handleUnauthorized();
  if (!response.ok) throw await toApiError(response);
  if (response.status === 204) return undefined as T;
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

/**
 * Spring Data serializes a Page either directly ({content, totalElements, number, size, ...}) or,
 * in VIA_DTO mode, as PagedModel ({content, page: {size, number, totalElements, totalPages}}).
 * Both are accepted so the ledger keeps working whichever mode the backend runs with.
 */
interface RawPage<T> {
  content?: T[];
  totalElements?: number;
  totalPages?: number;
  number?: number;
  size?: number;
  page?: { size?: number; number?: number; totalElements?: number; totalPages?: number };
}

function normalizePage<T>(raw: RawPage<T>): Page<T> {
  const meta = raw.page ?? raw;
  return {
    content: raw.content ?? [],
    totalElements: meta.totalElements ?? 0,
    totalPages: meta.totalPages ?? 0,
    number: meta.number ?? 0,
    size: meta.size ?? 0,
  };
}

// ---------------------------------------------------------------- auth

/** POST /api/v1/auth/login (public). The caller must check that the returned role is ADMIN. */
export function login(username: string, password: string): Promise<AuthResponse> {
  return request<AuthResponse>('/api/v1/auth/login', { method: 'POST', body: { username, password }, auth: false });
}

// ---------------------------------------------------------------- dashboard

/** GET /api/v1/admin/dashboard */
export function getDashboard(): Promise<Dashboard> {
  return request<Dashboard>('/api/v1/admin/dashboard');
}

// ---------------------------------------------------------------- players

/** GET /api/v1/admin/players?query=&page=&size= (username substring search, size capped at 100 by the backend) */
export function searchPlayers(query: string, page: number, size: number): Promise<PagedPlayers> {
  return request<PagedPlayers>('/api/v1/admin/players', { query: { query, page, size } });
}

/** GET /api/v1/admin/players/{id} (uncached profile) */
export function getPlayer(playerId: number): Promise<PlayerProfile> {
  return request<PlayerProfile>(`/api/v1/admin/players/${playerId}`);
}

// ---------------------------------------------------------------- economy

/** GET /api/v1/admin/players/{id}/wallet */
export function getWallet(playerId: number): Promise<WalletSnapshot> {
  return request<WalletSnapshot>(`/api/v1/admin/players/${playerId}/wallet`);
}

/** GET /api/v1/admin/players/{id}/transactions?page=&size= (size capped at 200 by the backend) */
export async function getTransactions(playerId: number, page: number, size: number): Promise<Page<TransactionView>> {
  const raw = await request<RawPage<TransactionView>>(`/api/v1/admin/players/${playerId}/transactions`, {
    query: { page, size },
  });
  return normalizePage(raw);
}

/** POST /api/v1/admin/players/{id}/grant — manual compensation, returns the updated wallet. */
export function grantResources(playerId: number, body: GrantRequest): Promise<WalletSnapshot> {
  return request<WalletSnapshot>(`/api/v1/admin/players/${playerId}/grant`, { method: 'POST', body });
}

// ---------------------------------------------------------------- progression

/** GET /api/v1/admin/players/{id}/sessions?limit= (newest first, limit capped at 100) */
export function getSessions(playerId: number, limit: number): Promise<SessionView[]> {
  return request<SessionView[]>(`/api/v1/admin/players/${playerId}/sessions`, { query: { limit } });
}

/** GET /api/v1/admin/players/{id}/progress */
export function getProgress(playerId: number): Promise<ProgressView> {
  return request<ProgressView>(`/api/v1/admin/players/${playerId}/progress`);
}

/** GET /api/v1/admin/anti-cheat/validators — validator names in execution order */
export function getValidators(): Promise<string[]> {
  return request<string[]>('/api/v1/admin/anti-cheat/validators');
}

// ---------------------------------------------------------------- telemetry

export interface PlayerEventsQuery {
  type?: string;
  /** ISO-8601 instants */
  from?: string;
  to?: string;
  page?: number;
  size?: number;
}

/** GET /api/v1/admin/players/{id}/events?type=&from=&to=&page=&size= */
export function getPlayerEvents(playerId: number, q: PlayerEventsQuery): Promise<EventPage> {
  return request<EventPage>(`/api/v1/admin/players/${playerId}/events`, {
    query: { type: q.type, from: q.from, to: q.to, page: q.page, size: q.size },
  });
}

/** GET /api/v1/admin/telemetry/outbox */
export function getOutboxStats(): Promise<OutboxStats> {
  return request<OutboxStats>('/api/v1/admin/telemetry/outbox');
}

// ---------------------------------------------------------------- live events

/** GET /api/v1/admin/events (newest first, with participants and top standings) */
export function listEvents(): Promise<LiveEventView[]> {
  return request<LiveEventView[]>('/api/v1/admin/events');
}

/** GET /api/v1/admin/events/{id} */
export function getEvent(id: number): Promise<LiveEventView> {
  return request<LiveEventView>(`/api/v1/admin/events/${id}`);
}

/** POST /api/v1/admin/events */
export function createEvent(body: CreateEventRequest): Promise<LiveEventView> {
  return request<LiveEventView>('/api/v1/admin/events', { method: 'POST', body });
}

/** POST /api/v1/admin/events/{id}/activate (SCHEDULED -> ACTIVE) */
export function activateEvent(id: number): Promise<LiveEventView> {
  return request<LiveEventView>(`/api/v1/admin/events/${id}/activate`, { method: 'POST' });
}

/** POST /api/v1/admin/events/{id}/end (ACTIVE -> ENDED, prizes paid) */
export function endEvent(id: number): Promise<LiveEventView> {
  return request<LiveEventView>(`/api/v1/admin/events/${id}/end`, { method: 'POST' });
}

/** POST /api/v1/admin/events/{id}/cancel */
export function cancelEvent(id: number): Promise<LiveEventView> {
  return request<LiveEventView>(`/api/v1/admin/events/${id}/cancel`, { method: 'POST' });
}

// ---------------------------------------------------------------- experiments

/** GET /api/v1/admin/experiments (newest first, with assignment counts) */
export function listExperiments(): Promise<ExperimentView[]> {
  return request<ExperimentView[]>('/api/v1/admin/experiments');
}

/** GET /api/v1/admin/experiments/{id} */
export function getExperiment(id: number): Promise<ExperimentView> {
  return request<ExperimentView>(`/api/v1/admin/experiments/${id}`);
}

/** POST /api/v1/admin/experiments */
export function createExperiment(body: CreateExperimentRequest): Promise<ExperimentView> {
  return request<ExperimentView>('/api/v1/admin/experiments', { method: 'POST', body });
}

/** POST /api/v1/admin/experiments/{id}/start (DRAFT|PAUSED -> RUNNING) */
export function startExperiment(id: number): Promise<ExperimentView> {
  return request<ExperimentView>(`/api/v1/admin/experiments/${id}/start`, { method: 'POST' });
}

/** POST /api/v1/admin/experiments/{id}/pause (RUNNING -> PAUSED) */
export function pauseExperiment(id: number): Promise<ExperimentView> {
  return request<ExperimentView>(`/api/v1/admin/experiments/${id}/pause`, { method: 'POST' });
}

/** POST /api/v1/admin/experiments/{id}/end */
export function endExperiment(id: number): Promise<ExperimentView> {
  return request<ExperimentView>(`/api/v1/admin/experiments/${id}/end`, { method: 'POST' });
}

// ---------------------------------------------------------------- remote config

/** GET /api/v1/admin/config (sorted by key) */
export function listConfig(): Promise<ConfigEntryView[]> {
  return request<ConfigEntryView[]>('/api/v1/admin/config');
}

/** PUT /api/v1/admin/config/{key} — creates or updates a key; players see it within the 60s cache TTL. */
export function updateConfig(key: string, body: UpdateConfigRequest): Promise<ConfigEntryView> {
  return request<ConfigEntryView>(`/api/v1/admin/config/${encodeURIComponent(key)}`, { method: 'PUT', body });
}

// ---------------------------------------------------------------- leaderboard

/** GET /api/v1/admin/leaderboards/current?limit= */
export function getCurrentLeaderboard(limit = 100): Promise<LeaderboardView> {
  return request<LeaderboardView>('/api/v1/admin/leaderboards/current', { query: { limit } });
}

/** GET /api/v1/admin/leaderboards/{season}?limit= (season like 2026-W36) */
export function getLeaderboardSeason(season: string, limit = 100): Promise<LeaderboardView> {
  return request<LeaderboardView>(`/api/v1/admin/leaderboards/${encodeURIComponent(season)}`, { query: { limit } });
}

/** POST /api/v1/admin/leaderboards/{season}/finalize?force= — pays the season prizes (idempotent). */
export function finalizeSeason(season: string, force: boolean): Promise<FinalizationResult> {
  return request<FinalizationResult>(`/api/v1/admin/leaderboards/${encodeURIComponent(season)}/finalize`, {
    method: 'POST',
    query: { force },
  });
}

// ---------------------------------------------------------------- levels

/** GET /api/v1/admin/levels?from=&to= — only levels that exist in MongoDB are returned. */
export function listLevels(from: number, to: number): Promise<LevelDefinition[]> {
  return request<LevelDefinition[]>('/api/v1/admin/levels', { query: { from, to } });
}

/** PUT /api/v1/admin/levels/{n} — hand-tuned definition, bumps the version and evicts the cache. */
export function upsertLevel(levelNumber: number, body: UpsertLevelRequest): Promise<LevelDefinition> {
  return request<LevelDefinition>(`/api/v1/admin/levels/${levelNumber}`, { method: 'PUT', body });
}

// ---------------------------------------------------------------- actuator (public endpoints)

/** Health endpoints answer 503 with a JSON body when DOWN, so the body is parsed whatever the status. */
async function fetchHealth(path: string): Promise<Health> {
  let response: Response;
  try {
    response = await fetch(API_BASE_URL + path, { headers: { Accept: 'application/json' } });
  } catch {
    throw new ApiError(0, 'NETWORK_ERROR', `Cannot reach the API at ${API_BASE_URL}`);
  }
  const text = await response.text();
  try {
    const body = JSON.parse(text) as Health;
    if (body && typeof body.status === 'string') return body;
  } catch {
    // not JSON: fall through
  }
  throw new ApiError(response.status, `HTTP_${response.status}`, text.slice(0, 200) || response.statusText || 'Health check failed');
}

/** GET /actuator/health */
export function getHealth(): Promise<Health> {
  return fetchHealth('/actuator/health');
}

/** GET /actuator/health/liveness */
export function getLiveness(): Promise<Health> {
  return fetchHealth('/actuator/health/liveness');
}

/** GET /actuator/health/readiness */
export function getReadiness(): Promise<Health> {
  return fetchHealth('/actuator/health/readiness');
}
