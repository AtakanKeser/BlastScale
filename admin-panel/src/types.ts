// TypeScript mirrors of the backend DTO records. The Java records are the contract, so the field
// names here must match them exactly. The backend serializes with Jackson "non_null" inclusion:
// Java fields that may be null can be missing from the JSON entirely, hence the optional fields.

/** POST /api/v1/auth/login response (security/dto/AuthResponse.java). */
export interface AuthResponse {
  token: string;
  expiresAt: string;
  playerId: number;
  username: string;
  role: string;
}

/** Uniform error body returned by every endpoint (common/api/ApiError.java). */
export interface ApiErrorBody {
  code: string;
  message: string;
  details?: Record<string, unknown>;
  timestamp?: string;
  path?: string;
}

/** Spring Data Page<T> as consumed by the panel (normalized in api.ts). */
export interface Page<T> {
  content: T[];
  totalElements: number;
  totalPages: number;
  number: number;
  size: number;
}

// ---------------------------------------------------------------- dashboard (admin/AdminDashboardController.java)

export interface DashboardHttp {
  requests: number;
  meanLatencyMillis: number;
  maxLatencyMillis: number;
  serverErrorRate: number;
}

export interface DashboardOutbox {
  pending: number;
  deadLettered: number;
}

export interface Dashboard {
  uptimeSeconds: number;
  http: DashboardHttp;
  players: number;
  levelStarts: number;
  /** completion result -> count, e.g. {"success": 10, "rejected": 1} */
  levelCompletions: Record<string, number>;
  /** validator name -> rejected count */
  completionRejections: Record<string, number>;
  cacheHitRate?: number | null;
  outbox: DashboardOutbox;
  activeEvents: number;
  runningExperiments: number;
  antiCheatValidators: string[];
}

// ---------------------------------------------------------------- players (player/PlayerAdminController.java, PlayerProfile.java)

export interface PlayerRow {
  id: number;
  username: string;
  role: string;
  currentLevel: number;
  guest: boolean;
  createdAt: string;
  lastSeenAt: string;
}

export interface PagedPlayers {
  players: PlayerRow[];
  total: number;
  page: number;
  size: number;
}

/** Wallet snapshot embedded in the profile (PlayerProfile.WalletSummary). */
export interface WalletSummary {
  coins: number;
  lives: number;
  maxLives: number;
  nextLifeInSeconds: number;
  stars: number;
  boosters: Record<string, number>;
}

export interface PlayerProfile {
  id: number;
  username: string;
  role: string;
  currentLevel: number;
  createdAt: string;
  wallet?: WalletSummary | null;
}

// ---------------------------------------------------------------- economy (economy/EconomyAdminController.java, dto/*)

export const RESOURCES = ['COIN', 'LIFE', 'STAR', 'BOOSTER_HAMMER', 'BOOSTER_SHUFFLE', 'BOOSTER_EXTRA_MOVES'] as const;
export type Resource = (typeof RESOURCES)[number];

/** economy/WalletSnapshot.java */
export interface WalletSnapshot {
  coins: number;
  lives: number;
  maxLives: number;
  nextLifeInSeconds: number;
  stars: number;
  boosters: Record<string, number>;
}

/** economy/dto/TransactionView.java */
export interface TransactionView {
  id: number;
  type: string;
  resource: string;
  amount: number;
  balanceAfter: number;
  reason: string;
  referenceId: string;
  createdAt: string;
}

/** economy/dto/GrantRequest.java; amount may be negative to take resources away. */
export interface GrantRequest {
  resource: Resource;
  amount: number;
  note?: string;
}

// ---------------------------------------------------------------- progression (progression/ProgressionAdminController.java, dto/*)

export interface SessionView {
  id: string;
  level: number;
  seed: number;
  status: string;
  startedAt: string;
  completedAt?: string | null;
  score?: number | null;
  movesUsed?: number | null;
  stars?: number | null;
  rewardCoins?: number | null;
  rewardStrategy?: string | null;
}

export interface LevelEntry {
  level: number;
  stars: number;
  bestScore: number;
  attempts: number;
  cleared: boolean;
  completedAt?: string | null;
}

export interface ProgressView {
  currentLevel: number;
  totalStars: number;
  levels: LevelEntry[];
}

// ---------------------------------------------------------------- telemetry (telemetry/TelemetryAdminController.java, TelemetryDocument.java)

export const TELEMETRY_EVENT_TYPES = [
  'PLAYER_REGISTERED',
  'LEVEL_STARTED',
  'LEVEL_COMPLETED',
  'LEVEL_FAILED',
  'COMPLETION_REJECTED',
  'ECONOMY_TRANSACTION',
  'DAILY_REWARD_CLAIMED',
  'BOOSTER_PURCHASED',
  'LIVES_PURCHASED',
  'LEADERBOARD_FINALIZED',
  'LEADERBOARD_REWARD_GRANTED',
  'EVENT_REWARD_GRANTED',
  'EVENT_FINALIZED',
  'EXPERIMENT_ASSIGNED',
  'CONFIG_UPDATED',
  'ADMIN_GRANT',
] as const;
export type TelemetryEventType = (typeof TELEMETRY_EVENT_TYPES)[number];

export interface TelemetryDocument {
  id: string;
  eventType: string;
  playerId?: number | null;
  aggregateType?: string | null;
  aggregateId?: string | null;
  timestamp: string;
  payload?: Record<string, unknown> | null;
}

/** TelemetrySearchService.EventPage */
export interface EventPage {
  events: TelemetryDocument[];
  total: number;
  page: number;
  size: number;
}

/** TelemetryAdminController.OutboxStats */
export interface OutboxStats {
  pending: number;
  deadLettered: number;
}

// ---------------------------------------------------------------- live events (event/LiveEventAdminController.java, dto/*)

export const LIVE_EVENT_TYPES = ['ROCKET_RACE', 'DOUBLE_REWARD'] as const;
export type LiveEventType = (typeof LIVE_EVENT_TYPES)[number];

/** SCHEDULED -> ACTIVE -> ENDED -> FINALIZED, plus CANCELLED from SCHEDULED/ACTIVE. */
export type LiveEventStatus = 'SCHEDULED' | 'ACTIVE' | 'ENDED' | 'FINALIZED' | 'CANCELLED';

export interface Standing {
  rank: number;
  playerId: number;
  name: string;
  points: number;
  rewardCoins?: number | null;
}

export interface LiveEventView {
  id: number;
  type: string;
  name: string;
  status: string;
  startAt: string;
  endAt: string;
  configuration: Record<string, unknown>;
  participants?: number | null;
  top?: Standing[] | null;
  createdAt: string;
  updatedAt: string;
}

/** event/dto/CreateEventRequest.java; startAt null = start immediately. */
export interface CreateEventRequest {
  type: LiveEventType;
  name: string;
  startAt?: string | null;
  endAt: string;
  configuration: Record<string, unknown>;
}

// ---------------------------------------------------------------- experiments (experiment/ExperimentAdminController.java, dto/*)

/** DRAFT -> RUNNING <-> PAUSED -> ENDED */
export type ExperimentStatus = 'DRAFT' | 'RUNNING' | 'PAUSED' | 'ENDED';

/** experiment/ExperimentVariant.java; weights of one experiment sum to 100. */
export interface ExperimentVariant {
  name: string;
  weight: number;
  overrides: Record<string, unknown>;
}

export interface ExperimentView {
  id: number;
  key: string;
  name: string;
  status: string;
  startAt?: string | null;
  endAt?: string | null;
  variants: ExperimentVariant[];
  /** variant name -> assigned players (admin views only) */
  assignments?: Record<string, number> | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateExperimentRequest {
  key: string;
  name: string;
  variants: ExperimentVariant[];
  startAt?: string | null;
  endAt?: string | null;
}

// ---------------------------------------------------------------- remote config (remoteconfig/ConfigAdminController.java, dto/*)

export interface ConfigEntryView {
  key: string;
  value: unknown;
  description?: string | null;
  updatedAt: string;
  updatedBy?: string | null;
}

/** Body of PUT /api/v1/admin/config/{key}; value may be any JSON value except null. */
export interface UpdateConfigRequest {
  value: unknown;
  description?: string | null;
}

/** Well-known keys from remoteconfig/ConfigKeys.java (used as suggestions when adding a key). */
export const KNOWN_CONFIG_KEYS = [
  'dailyRewardCoins',
  'dailyRewardStreakBonus',
  'maxLives',
  'lifeRegenerationMinutes',
  'lifeRefillPrice',
  'boosterPrices',
  'startingCoins',
  'levelCompleteBaseCoins',
  'coinsPerStar',
  'firstClearBonusCoins',
  'rewardMultiplier',
  'rocketRaceEnabled',
  'leaderboardEnabled',
] as const;

// ---------------------------------------------------------------- leaderboard (leaderboard/LeaderboardAdminController.java, dto/*)

export interface LeaderboardEntry {
  rank: number;
  playerId: number;
  name: string;
  score: number;
}

export interface LeaderboardView {
  season: string;
  endsAt: string;
  finalized: boolean;
  players: LeaderboardEntry[];
  myRank?: number | null;
  myScore: number;
}

export interface RewardedPlayer {
  rank: number;
  playerId: number;
  score: number;
  coins: number;
}

export interface FinalizationResult {
  season: string;
  alreadyFinalized: boolean;
  finalizedAt: string;
  participants: number;
  rewards: RewardedPlayer[];
}

// ---------------------------------------------------------------- levels (level/LevelAdminController.java, LevelDefinition.java, dto/UpsertLevelRequest.java)

export interface LevelDefinition {
  id: string;
  levelNumber: number;
  version: number;
  rows: number;
  cols: number;
  colorCount: number;
  moveLimit: number;
  targetScore: number;
  starThresholds: number[];
  specialRules?: Record<string, unknown> | null;
  source?: string | null;
  updatedAt?: string | null;
}

export interface UpsertLevelRequest {
  rows: number;
  cols: number;
  colorCount: number;
  moveLimit: number;
  targetScore: number;
  /** exactly 3 values, the first equal to targetScore */
  starThresholds: number[];
  specialRules?: Record<string, unknown> | null;
}

// ---------------------------------------------------------------- actuator health

export interface HealthComponent {
  status: string;
  details?: Record<string, unknown>;
  components?: Record<string, HealthComponent>;
}

/** GET /actuator/health (show-details: always) and the liveness/readiness groups. */
export interface Health {
  status: string;
  components?: Record<string, HealthComponent>;
  groups?: string[];
}

// ---------------------------------------------------------------- prometheus HTTP API

export interface PromResult {
  metric: Record<string, string>;
  /** [unix timestamp, sample value as string] */
  value: [number, string];
}

export interface PromQueryResponse {
  status: 'success' | 'error';
  data?: { resultType: string; result: PromResult[] };
  errorType?: string;
  error?: string;
}
