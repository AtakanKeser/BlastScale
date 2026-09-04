# BlastScale

[![CI](https://github.com/AtakanKeser/BlastScale/actions/workflows/ci.yml/badge.svg)](https://github.com/AtakanKeser/BlastScale/actions/workflows/ci.yml)

BlastScale is a production-oriented backend platform for a casual mobile puzzle game, designed to
explore the engineering challenges that emerge when gameplay systems scale to large player
populations.

The game itself is deliberately small (an 8x8 "tap two or more blocks of the same colour" blast
puzzle). The interesting part is everything around it: a server-authoritative economy, an
anti-cheat pipeline that replays the player's moves, exactly-once rewards under retries and
concurrency, a Redis leaderboard, live events, remote configuration and A/B experiments that
change the game without a release, a transactional outbox feeding Elasticsearch for player
investigations, and Prometheus/Grafana observability — all runnable with one command.

```
docker compose up --build
```

| What | Where |
|------|-------|
| API (through the nginx load balancer) | http://localhost:8080 |
| Admin / LiveOps panel (`admin` / `admin12345`) | http://localhost:3001 |
| Grafana (`admin` / `admin`) — dashboard "BlastScale Overview" | http://localhost:3000 |
| Prometheus | http://localhost:9090 |
| Health probes | http://localhost:8080/actuator/health/readiness, `/liveness` |

## Contents

- [Architecture](#architecture)
- [Engineering concerns and where they live](#engineering-concerns-and-where-they-live)
- [Quick start](#quick-start)
- [API](#api)
- [Gameplay and anti-cheat](#gameplay-and-anti-cheat)
- [Economy and consistency model](#economy-and-consistency-model)
- [Caching strategy](#caching-strategy)
- [LiveOps: events, remote config, experiments](#liveops-events-remote-config-experiments)
- [Telemetry and investigation](#telemetry-and-investigation)
- [Observability](#observability)
- [Scaling strategy](#scaling-strategy)
- [Failure scenarios](#failure-scenarios)
- [Testing strategy](#testing-strategy)
- [Benchmark results](#benchmark-results)
- [Clients](#clients)
- [Repository layout](#repository-layout)
- [Architecture decisions](#architecture-decisions)

## Architecture

```
              ┌──────────────┐        ┌───────────────┐
              │ Unity client │        │  Admin panel  │
              └───────┬──────┘        └───────┬───────┘
                      │  REST / JSON          │
                      ▼                       ▼
              ┌──────────────────────────────────────┐
              │        nginx (load balancer)         │
              └───────┬──────────┬───────────┬───────┘
                      ▼          ▼           ▼
                   api-1       api-2       api-3      Spring Boot, stateless
                      └──────────┼───────────┘
              ┌──────────────────┼──────────────────┐
              ▼                  ▼                  ▼
           MySQL              Redis             MongoDB
     players, wallets,   profile cache,     level definitions
     ledger, sessions,   weekly leaderboard,
     progress, events,   rate limits,
     experiments,        idempotency keys,
     outbox              job locks
              │
              │  outbox worker (SKIP LOCKED batches)
              ▼
        Elasticsearch  ◄── admin: "show me everything that happened to player 123"
     telemetry index

           Prometheus ──► Grafana      (scrapes every replica via Docker DNS)
```

The backend is a **modular monolith**. I intentionally chose a modular monolith because the
current domain does not justify the operational complexity of microservices. Modules have explicit
boundaries and can be extracted independently if traffic or ownership requirements change.

```
com.atakan.blastscale
├── player        accounts, guest login, cached profile
├── security      JWT resource server, rate limiting, admin bootstrap
├── economy       wallet, append-only ledger, lives regeneration, daily reward, shop, reward strategies
├── level         deterministic board engine + MongoDB level definitions
├── progression   level sessions, anti-cheat validation chain, idempotent completion
├── leaderboard   weekly Redis sorted set + idempotent finalization job
├── event         live events (Rocket Race, Double Reward) driven by JSON rules
├── experiment    deterministic A/B bucketing with sticky assignments
├── remoteconfig  runtime tuning values + per-player resolution
├── telemetry     transactional outbox -> Elasticsearch, investigation queries
├── admin         dashboard aggregates
└── common        error contract, idempotency, cache-aside, distributed lock, metrics, clock
```

Modules talk to each other through service classes or Spring application events
(`PlayerRegisteredEvent` creates the wallet, `WalletChangedEvent` evicts the cached profile), never
through another module's repositories.

## Engineering concerns and where they live

| Concern | Implementation | Code |
|---------|----------------|------|
| Duplicate requests | `Idempotency-Key` header, Redis at-most-once guard, `Idempotent-Replayed` response header | `common/idempotency` |
| Exactly-once rewards | conditional `UPDATE ... WHERE status = ACTIVE` + unique ledger key, on top of idempotency | `progression/ProgressionService`, `economy/EconomyService` |
| Cheat prevention | Chain of Responsibility: session → progression → duration → score bounds → server-side replay | `progression/validation` |
| Player economy consistency | `SELECT ... FOR UPDATE`, append-only ledger with `balance_after`, `@Version` guard | `economy` |
| Reward rules | Strategy pattern: experiment > live event > standard | `economy/reward` |
| Leaderboard | Redis sorted set per ISO week, finalization under a Redis lock | `leaderboard` |
| Live events | JSON rules parsed into typed `EventRule`s; scheduler with lock; prizes via ledger | `event` |
| Remote configuration | key/value table cached 60 s, admin edits without deploys | `remoteconfig` |
| A/B experiments | `SHA-256(playerId:key) % 100` over cumulative weights, sticky persisted assignment | `experiment` |
| Caching | cache-aside helper that degrades to the source and exports hit/miss/error metrics | `common/redis/RedisJsonCache` |
| Concurrency | 100-thread completion test, 60-thread wallet overdraw test | `integration/*ConcurrencyIntegrationTest` |
| Failure recovery | outbox retries, fail-open rate limiting, procedural level fallback, readiness on MySQL only | see [Failure scenarios](#failure-scenarios) |
| Monitoring | Micrometer custom metrics, Prometheus histograms, Grafana dashboard as code | `common/metrics`, `infra/grafana` |

## Quick start

Requirements: Docker with ~6 GB of memory available to containers.

```bash
cp .env.example .env            # optional: change the JWT secret / admin password
docker compose up --build       # API, admin panel, MySQL, Redis, MongoDB, Elasticsearch, Prometheus, Grafana
```

Try the API (register, start a level, look at your wallet):

```bash
TOKEN=$(curl -s -X POST localhost:8080/api/v1/auth/register -H 'Content-Type: application/json' \
  -d '{"username":"demo_player","password":"password123"}' | sed -E 's/.*"token":"([^"]+)".*/\1/')

curl -s localhost:8080/api/v1/players/me -H "Authorization: Bearer $TOKEN"
curl -s -X POST localhost:8080/api/v1/levels/1/start -H "Authorization: Bearer $TOKEN"
```

Completing a level requires a legal move list for the seed you were given — that is the whole
point of the server-side replay. The quickest way to see the full flow is the k6 smoke test, which
plays levels with the JavaScript port of the engine:

```bash
docker compose --profile loadtest run --rm --no-deps k6 run /scripts/smoke.js
```

Scale the API horizontally (nginx round-robins across replicas, Prometheus discovers them via DNS):

```bash
docker compose up --build --scale api=3
```

Run the backend without Docker for the app itself (stores still in containers):

```bash
docker compose up -d mysql redis mongo elasticsearch
cd backend && ./mvnw spring-boot:run
```

## API

All endpoints are under `/api/v1`, JSON in and out, `Authorization: Bearer <jwt>` except `auth/*`.
Errors always have the same shape:

```json
{ "code": "NO_LIVES_LEFT", "message": "You have no lives left. Next life in 1240 seconds",
  "details": { "nextLifeInSeconds": 1240 }, "timestamp": "2026-09-04T12:00:00Z", "path": "/api/v1/levels/42/start" }
```

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/auth/register`, `/auth/login`, `/auth/guest` | account + JWT (guest login keyed by device id) |
| GET | `/players/me` | profile with wallet (Redis cache-aside) |
| GET | `/config` | remote configuration resolved for the caller + experiment assignments |
| POST | `/levels/{n}/start` | consume a life, get session id + board seed + rules |
| POST | `/levels/{n}/complete` | submit moves; server replays, rewards, advances (send `Idempotency-Key`) |
| POST | `/levels/{n}/fail` | lost level; charges used boosters |
| GET | `/progress` | level map data |
| GET | `/economy/wallet`, `/economy/transactions` | balances and personal ledger |
| GET/POST | `/economy/daily-reward` | status / claim (streak bonus) |
| POST | `/economy/shop/boosters`, `/economy/shop/lives` | purchases priced by remote config |
| GET | `/leaderboards/weekly` | top 100 + own rank of the current ISO week |
| GET | `/events` | active live events with own points and rank |
| GET | `/levels/{n}` | level definition preview |
| * | `/admin/**` | LiveOps: players, ledger, sessions, telemetry, events, experiments, config, leaderboard, levels, dashboard (role ADMIN) |

The level completion request carries the **moves**, not the outcome:

```json
POST /api/v1/levels/42/complete
Idempotency-Key: 2afd983e-…
{ "sessionId": "abc123", "score": 12450, "movesUsed": 17,
  "moves": [ {"type": "TAP", "row": 3, "col": 4}, {"type": "HAMMER", "row": 0, "col": 7}, … ],
  "extraMovesUsed": false }
```

```json
{ "status": "COMPLETED", "level": 42, "score": 12450, "stars": 2, "firstClear": true,
  "reward": { "coins": 250, "stars": 2, "multiplier": 2.0, "strategy": "DOUBLE_REWARD_EVENT" },
  "wallet": { "coins": 1875, "lives": 4, "maxLives": 5, "nextLifeInSeconds": 1710, "stars": 37, "boosters": {"HAMMER": 1, "SHUFFLE": 0, "EXTRA_MOVES": 2} },
  "nextLevel": 43, "eventPoints": [ { "eventId": 7, "name": "Rocket Race", "points": 1, "totalPoints": 12 } ] }
```

A retried request with the same `Idempotency-Key` returns this exact body with
`Idempotent-Replayed: true`; a new request for the same session returns `"status": "ALREADY_PROCESSED"`.
Either way the coins are paid once.

## Gameplay and anti-cheat

The client never decides the outcome. `POST /levels/{n}/start` returns a **server-chosen seed**;
the board is generated from it by a tiny deterministic engine (32-bit LCG + blast rules) that exists
three times — Java on the server, C# in the Unity client, JavaScript in the k6 scripts — and
`docs/engine/engine-vectors.json` holds 41 golden cases every port must reproduce bit for bit.

```
validate session  ──►  validate progression  ──►  validate duration  ──►  validate score bounds  ──►  replay moves
 owner / ACTIVE /       level unlocked            >= 150 ms per move       moves <= limit,             server score is
 level / not expired                                                       score <= theoretical max,   the only score;
                                                                           boosters owned              objective reached?
```

`CompletionValidationChain` runs these `CompletionValidator` beans in `@Order` (Chain of
Responsibility). New rules can be added without changing the completion orchestration logic.
Rejections are counted per validator (`blastscale_completion_rejected_total{validator=…}`) and
recorded as `COMPLETION_REJECTED` telemetry so cheat patterns are visible.

Then, in a single MySQL transaction: claim the session (`UPDATE … WHERE status = 'ACTIVE'`), update
level progress, calculate the reward (Strategy pattern: `ExperimentRewardStrategy` >
`DoubleRewardEventStrategy` > `StandardRewardStrategy`), apply coins/stars/booster debits through
the ledger, advance the player, award Rocket Race points, write the `LEVEL_COMPLETED` outbox event.
Only after commit is the Redis leaderboard updated, so a rolled-back completion never leaves a
phantom score.

## Economy and consistency model

Balances are a cache of the ledger:

```
economy_transaction
id  player_id  type    resource  amount  balance_after  reason          reference_id
1   123        CREDIT  COIN      +500    500            INITIAL_GRANT   player:123
2   123        DEBIT   LIFE      -1      4              LEVEL_START     8d1c…(session)
3   123        CREDIT  COIN      +125    625            LEVEL_COMPLETE  8d1c…(session)
4   123        DEBIT   COIN      -200    425            BUY_BOOSTER     b1
```

Every change runs the same recipe in `EconomyService.apply`:

```
BEGIN
  SELECT player_wallet FOR UPDATE        -- serialise per player, never globally
  regenerate lives lazily                -- floor((now - lives_updated_at) / interval)
  reject duplicate (reason, reference)   -- exactly-once
  check every debit is covered           -- never negative
  INSERT economy_transaction (+ balance_after)
  UPDATE player_wallet (version + 1)     -- optimistic guard on top of the row lock
  INSERT outbox_event                    -- telemetry in the same transaction
COMMIT
```

Guarantees, in decreasing order of how often they are needed:

1. **Idempotency-Key** (Redis): retried requests return the stored response.
2. **Conditional session update**: only one of N concurrent completions moves a session out of
   `ACTIVE`; the rest get `ALREADY_PROCESSED`.
3. **Unique ledger key** `(player_id, reason, reference_id, resource)`: even if Redis is gone and
   two replicas race, MySQL refuses a second `LEVEL_COMPLETE` for the same session.

Concurrent purchases from one wallet queue on the row lock; a 60-thread test asserts the wallet
never goes negative and `SUM(ledger.amount) == balance`. Lives regenerate lazily from a timestamp
(no scheduler touching millions of rows), and the interval comes from remote config — which is
what the sample A/B experiment (30 vs 25 minutes) changes per variant.

## Caching strategy

| Data | Store | TTL | Invalidation | Miss / Redis down |
|------|-------|-----|--------------|--------------------|
| Player profile | Redis `player:{id}` | 10 min | `WalletChangedEvent` after commit, level advance | MySQL |
| Remote config | Redis `config:base` | 60 s | admin update | MySQL |
| Live experiments / assignments | Redis, per-player key includes a fingerprint of the live set | 60 s | admin change = new fingerprint | MySQL |
| Active live events | Redis `events:active` | 30 s | admin change; window re-checked on read | MySQL |
| Level definitions | Redis `level:{n}` | 10 min | admin upsert | MongoDB, then procedural |

All of it is the same `RedisJsonCache` cache-aside helper: JSON values (readable with `redis-cli`
during an incident), every Redis failure swallowed and logged, hit/miss/error counters per cache.

## LiveOps: events, remote config, experiments

**Live events** are rows, not deploys. A Rocket Race is:

```json
{ "type": "ROCKET_RACE", "name": "Rocket Race", "endAt": "2026-09-07T00:00:00Z",
  "configuration": { "pointsPerLevel": 1, "minimumLevel": 20, "rewards": { "1": 10000, "2": 5000, "3": 3000 } } }
```

Points are added with an atomic `INSERT … ON DUPLICATE KEY UPDATE` inside the completion
transaction. A `DOUBLE_REWARD` event switches the reward strategy for everyone the moment it is
active. The scheduler (Redis-locked, one replica per tick) activates and ends events by time;
finalization pays prizes through the ledger with reference `event:{id}`, so a crashed run can be
retried without double payment.

**Remote configuration** (`GET /api/v1/config`) returns values such as `dailyRewardCoins`,
`maxLives`, `lifeRegenerationMinutes`, `boosterPrices`, `rocketRaceEnabled`. Change
`dailyRewardCoins` from 100 to 150 in the admin panel and every client sees it within 60 s
without an update.

**Experiments** assign players deterministically: `bucket = SHA-256(playerId + ":" + key) % 100`,
mapped over cumulative variant weights (A: 0–49, B: 50–99). The assignment is persisted with
`INSERT IGNORE` (race-free between two devices of one account) so it stays sticky even if weights
are edited later, and it is recorded as `EXPERIMENT_ASSIGNED` telemetry. Variants carry config
overrides, which is how one experiment can change the life timer and another the reward
multiplier — the resolved config for the player is what the game logic reads.

## Telemetry and investigation

Every significant action (`LEVEL_STARTED`, `LEVEL_COMPLETED`, `COMPLETION_REJECTED`,
`ECONOMY_TRANSACTION`, `DAILY_REWARD_CLAIMED`, `EXPERIMENT_ASSIGNED`, `EVENT_REWARD_GRANTED`, …)
is written as an `outbox_event` row **in the same transaction** as the change it describes. The
`OutboxPublisherJob` drains the table with `FOR UPDATE SKIP LOCKED` batches (safe with several
replicas), bulk-indexes into Elasticsearch and marks rows published, retrying with a bounded
attempt count. Document ids equal outbox ids, so redelivery is harmless.

Support ticket: "I finished level 412 but never got my reward."

```
GET /api/v1/admin/players/123/events?type=LEVEL_COMPLETED
GET /api/v1/admin/players/123/events?from=2026-09-04T10:00:00Z
GET /api/v1/admin/players/123/transactions
GET /api/v1/admin/players/123/sessions
```

…shows the session, the validator that rejected it (if any), the ledger entry and its balance.
If Elasticsearch was down at the time, the events are still in the outbox and arrive when it is
back — `blastscale_outbox_pending` on the dashboard tells you how far behind it is.

## Observability

`/actuator/prometheus` exports the standard HTTP/JVM/HikariCP/Tomcat metrics plus:

| Metric | Meaning |
|--------|---------|
| `blastscale_level_start_total` | sessions created |
| `blastscale_level_completion_total{result=success\|replayed\|rejected\|failed}` | outcome of completion requests |
| `blastscale_completion_rejected_total{validator}` | which anti-cheat rule fired |
| `blastscale_reward_processing_duration_seconds` (histogram) | validate → replay → reward → persist |
| `blastscale_economy_transaction_total{resource,type}` | ledger throughput |
| `blastscale_cache_requests_total{cache,result=hit\|miss\|error}` | cache effectiveness, Redis health |
| `blastscale_idempotent_replay_total{scope}` | how often clients retry |
| `blastscale_rate_limit_rejected_total` | abuse / misbehaving clients |
| `blastscale_outbox_pending`, `_published_total`, `_failed_total` | telemetry pipeline health |

`http.server.requests` is exported with percentile histograms, so Grafana computes p50/p95/p99
with `histogram_quantile` across all replicas. The dashboard (`infra/grafana/dashboards/blastscale.json`)
is generated by `infra/grafana/generate-dashboard.py` — dashboard as code.

Health: `/actuator/health/liveness` (process alive) and `/actuator/health/readiness` (MySQL
reachable). Readiness deliberately excludes Redis, MongoDB and Elasticsearch: each of them degrades
a feature rather than the service, and taking every replica out of rotation because the telemetry
store is slow would turn a partial degradation into an outage.

## Scaling strategy

- The API is stateless: JWTs are self-contained, there is no HTTP session, every replica can serve
  every request. `docker compose up --scale api=3` puts three replicas behind nginx.
- Background jobs coordinate with Redis locks (`SET NX PX` + token-checked release) and the outbox
  uses `SKIP LOCKED`, so replicas never duplicate work. Jobs can be switched off on serving-only
  replicas with `blastscale.jobs.enabled=false`.
- Hot paths are one row lock per player: contention only grows with concurrent requests of the
  same player, not with player count.
- Where it would break first, and what I would do:
  1. **MySQL writes** (every completion is ~5 inserts/updates): the ledger is append-only and
     partitionable by `player_id`; the natural next step is sharding by player id or moving the
     `economy` + `progression` modules to their own database, which the module boundaries allow.
  2. **Redis leaderboard**: one sorted set per week is fine up to millions of members; beyond that,
     shard by league/bucket and merge top-N.
  3. **Outbox table growth**: published rows are purged daily; at very high volume a message broker
     (Kafka) replaces the polling worker without changing producers.
  4. **Elasticsearch ingest**: bulk size and publisher parallelism are the knobs; indexing is
     off the request path already.
  5. **Per-request config resolution**: two Redis reads; could become an in-process cache with
     short TTL if Redis round trips dominate.

## Failure scenarios

| Failure | Behaviour | Why it is safe |
|---------|-----------|----------------|
| Redis down | profile served from MySQL, rate limiting fails open, idempotency falls back to DB uniqueness, leaderboard returns `503 LEADERBOARD_UNAVAILABLE`, jobs skip their tick | no correctness depends on Redis; readiness stays UP |
| Elasticsearch down | gameplay unaffected; outbox rows accumulate and are published when it returns | transactional outbox, bounded retries, backlog metric |
| MongoDB down | procedural level definitions after a 2 s timeout | levels are derived from the level number; designers' tweaks return with MongoDB |
| MySQL down | writes stop, readiness → DOWN, instance leaves the load balancer | it is the source of truth; nothing is faked |
| Same request twice | `Idempotent-Replayed: true` with the stored response | Redis guard + DB constraints |
| Two devices complete the same session | one `COMPLETED`, one `ALREADY_PROCESSED`, one ledger entry | conditional UPDATE + unique key (tested with 100 threads) |
| Finalization job crashes halfway | next run skips players already paid and completes the season | ledger reference per season, completion row written last |
| Instance killed mid-request | transaction rolls back; retry with the same key completes it | graceful shutdown, no partial state |
| Client sends an impossible score | `422 SCORE_MISMATCH` / `SCORE_OUT_OF_RANGE`, counted per validator | server-side replay |

## Testing strategy

```bash
cd backend
./mvnw test      # 54 unit tests, seconds
./mvnw verify    # + Testcontainers integration tests (MySQL, Redis, MongoDB, Elasticsearch)
```

Unit tests cover the pure logic: engine rules and the golden vectors, life regeneration
arithmetic, reward strategies, ISO-week seasons, deterministic bucketing and its distribution,
each anti-cheat validator and the chain's short-circuit, event rule parsing, config coercion,
procedural level difficulty (the greedy bot must clear early levels ≥ 95 % of the time).

Integration tests boot the real application against real stores (started once per JVM) and go
through HTTP:

| Test | What it proves |
|------|----------------|
| `LevelCompletionIntegrationTest` | full flow, replay header, `ALREADY_PROCESSED`, single ledger row, tampering rejections, locked levels, foreign sessions, life consumption and regeneration with a manipulated clock, failing a level |
| `ConcurrentRewardIntegrationTest` | **100 threads complete one session simultaneously — exactly one reward**, with different keys and with the same key |
| `EconomyConcurrencyIntegrationTest` | 60 concurrent purchases never overdraw the wallet; ledger reconciles with the balance |
| `LeaderboardIntegrationTest` | ranks, own rank, forced finalization pays once, second run is a no-op |
| `LiveEventIntegrationTest` | double reward strategy switches at runtime; Rocket Race points, ranking and prizes paid once |
| `RemoteConfigExperimentIntegrationTest` | config edits visible immediately; sticky variant assignment with overrides; ending an experiment removes it |
| `OutboxTelemetryIntegrationTest` | events reach Elasticsearch and are searchable per player and type; backlog drains to zero |
| `DailyRewardIntegrationTest` | one claim per UTC day, streak bonus, streak reset, idempotent replay |
| `RateLimitIntegrationTest` | 429 with `Retry-After` once the per-minute budget is spent |
| `HealthAndMetricsIntegrationTest` | probes, Prometheus scrape contents, 401/403 contract |

Tests replace the application `Clock` with a mutable one, so "advance 31 minutes, a life came
back" and "next day, streak is 2" are deterministic.

## Benchmark results

Scripts and how to run them: [load-test/README.md](load-test/README.md). The scenario is the real
player loop — guest login → config → profile → start level → think (≈ 0.3 s + 0.2 s per move, the
server rejects anything faster than 150 ms per move) → solve with the JavaScript engine → complete
(with `Idempotency-Key`) → wallet → leaderboard — so every request goes through the full stack
including the server-side replay, the wallet row lock, the ledger and the outbox.

**Environment.** MacBook Pro (Apple M1 Pro, 10 cores, 16 GB), Docker Desktop with 8 GB / 10 CPUs
for the VM, everything in containers on the same machine: the API replicas, MySQL, Redis, MongoDB,
Elasticsearch, Prometheus, Grafana, nginx **and k6 itself** — which matters, see the analysis.
Each run: 90 s at a constant number of virtual users, one guest player per VU, `mem_limit: 1g`
per API replica.

| API replicas | VUs | req/s | p50 | p95 | p99 | failed requests | levels completed |
|---|---|---|---|---|---|---|---|
| 1 (before the deadlock fix) | 100 | 221 | 5.9 ms | 35 ms | 329 ms | 0.56 % (HTTP 500, InnoDB deadlock) | 3 352 |
| 1 | 100 | 215 | 8.9 ms | 119 ms | 437 ms | 0 % | 3 326 |
| 1 | 500 | 418 | 523 ms | 2.06 s | 3.34 s | 0 % | 6 785 |
| 1 | 1000 | 258 | 2.88 s | 6.67 s | 8.48 s | 0.11 % (host saturated) | 4 412 |
| 3 × 1.5 GiB | 100 | 203 | 11.9 ms | 150 ms | 1.50 s | 0 % | 3 141 |
| 3 × 1 GiB (GC-starved) | 100 | 175 | 25.9 ms | 429 ms | 1.52 s | 0 % | 2 692 |
| 3 × 1 GiB (GC-starved) | 500 | 236 | 778 ms | 4.49 s | 7.34 s | 0.93 % | 3 670 |

Per-step server round trips for the single-replica 100 VU run (p50 / p95): start level 10 / 119 ms,
complete level 19 / 162 ms, profile 5.6 / 63 ms, wallet 3.5 / 30 ms, leaderboard 5.2 / 43 ms.
Raw k6 summaries are written to `load-test/results/` by every run.

**What the numbers say**

- The **first run found a real bug**: 0.56 % of level starts failed with HTTP 500 — an InnoDB
  deadlock between the "abandon open sessions" `UPDATE` (served from the `(status, started_at)`
  index and therefore next-key-locking a range that spanned other players' rows) and concurrent
  session inserts. Fixed with a `(player_id, status)` index, `READ COMMITTED` isolation and a
  bounded retry of the deadlock victim (commit `fix(progression): resolve MySQL deadlock…`). Every
  run after that has 0 failed requests.
- Above ~100 VUs the **laptop, not the service, is the limit**: at 500 VUs the k6 container alone
  burns 5–6 of the 10 cores (its per-level solver slows from 9 ms to 86 ms, a CPU-starvation
  signal) and shares the rest with three JVMs, MySQL and Elasticsearch. Latency rises steeply while
  throughput barely doubles — classic saturation of the host. A meaningful capacity number needs
  the load generator on a different machine; these figures are a floor, not a ceiling.
- The profile cache shows a low hit rate under this loop because every iteration changes the
  wallet (start consumes a life, complete pays coins) right before the next profile read, which
  evicts the entry. Real home-screen traffic reads the profile far more often than it changes.
- **Three replicas on one laptop do not add capacity** — they share the same 10 cores with the
  load generator and the stores, so the 100 VU figures are the same within noise. What the
  scale-out runs did show is that *memory sizing matters as much as replica count*: with a 1 GiB
  limit per JVM the replicas sat at 90–100 % of it and GC-thrashed (p95 429 ms); at 1.5 GiB the
  same load ran at p95 150 ms. Horizontal scaling is proven functionally (nginx round-robin,
  Prometheus discovering every replica, Redis-locked jobs), its throughput benefit needs real
  hardware.
- `login` p90 is high at the start of each run: all VUs create their guest account in the same
  second on freshly started, cold JVMs (registration = player row + wallet + ledger + outbox in
  one transaction). Spreading VU start with a ramp removes it.

## Clients

- **Unity client** (`unity-client/`): login, home (level, lives with countdown, coins, stars),
  gameplay grid, result, shop, leaderboard and events screens, all talking to the real API. It runs
  the C# port of the engine for rendering only; the server decides the outcome by replaying the
  moves. See [unity-client/README.md](unity-client/README.md).
- **Admin / LiveOps panel** (`admin-panel/`, React): dashboard (Prometheus + business counters),
  player investigation (profile, ledger, sessions, telemetry timeline, compensation grants), live
  events, experiments, remote configuration, leaderboard, levels, system health. See
  [admin-panel/README.md](admin-panel/README.md).

## Playing on an iPhone

The Unity client runs on a real device with a development signature; `unity-client/build-ios.sh`
does the whole chain (Unity export → `xcodebuild` with automatic signing → install → launch):

```bash
docker compose up -d                                  # the phone talks to the backend on this Mac
xcrun devicectl list devices                          # copy the device identifier
BLASTSCALE_IOS_TEAM_ID=<your Apple team id> BLASTSCALE_IOS_DEVICE=<device id> ./unity-client/build-ios.sh
```

Requirements: Xcode with the iOS SDK, Unity 6000.3 with iOS Build Support, your Apple ID signed in
to Xcode (the team id is the `OU` of your "Apple Development" certificate:
`security find-certificate -c "Apple Development" -p | openssl x509 -noout -subject`), Developer Mode
enabled on the phone, and the phone **unlocked** while the script installs. The script bakes this
Mac's LAN address (`http://<lan-ip>:8080`) into the app as the default server, so phone and Mac must
be on the same Wi-Fi; override it with `BLASTSCALE_SERVER_URL`, or change it on the login screen.
Development builds allow plain HTTP for that reason (`InsecureHttpOption.AlwaysAllowed` in
`IosBuild.cs`); a store build would use HTTPS. Apps signed with a free personal team expire after
seven days — just run the script again.

## Repository layout

```
backend/        Spring Boot modular monolith (Java 21, Maven wrapper, Flyway migrations, tests)
admin-panel/    React + TypeScript LiveOps console
unity-client/   Unity 6 project (C# engine port + UGUI screens)
load-test/      k6 scenarios + JavaScript engine port + parity check
infra/          nginx, Prometheus, Grafana provisioning and dashboard generator
docs/adr/       architecture decision records
docs/engine/    golden engine vectors shared by all three engine implementations
docker-compose.yml
```

## Architecture decisions

The reasoning behind the main choices is written up as ADRs in [docs/adr](docs/adr/README.md):
modular monolith, MySQL ledger as the source of truth, Redis only for problems that need it,
MongoDB for configuration documents, transactional outbox to Elasticsearch, server-authoritative
gameplay, exactly-once rewards.

## Stack

Java 21 · Spring Boot 4.1 (Web MVC, Data JPA, Data Redis, Data MongoDB, Data Elasticsearch,
Security, Actuator) · MySQL 8.4 · Redis 7.4 · MongoDB 8 · Elasticsearch 9.1 · Flyway · Micrometer ·
Prometheus · Grafana · Testcontainers 2 · JUnit 6 · Mockito · k6 · Docker Compose · GitHub Actions ·
Unity 6 (C#) · React + Vite + TypeScript
