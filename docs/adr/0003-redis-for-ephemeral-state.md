# ADR 0003: Redis only where it solves a real problem

## Context
Redis is easy to add "for the CV". Every Redis dependency is also a new failure mode.

## Decision
Redis is used for five concrete problems, each with an explicit degradation path:

| Use | Keys | If Redis is down |
|-----|------|------------------|
| Player profile cache (cache-aside, 10 min TTL) | `player:{id}` | read from MySQL |
| Weekly leaderboard (sorted set) | `lb:weekly:{season}` | endpoint returns 503 `LEADERBOARD_UNAVAILABLE`; gameplay unaffected |
| Rate limiting (fixed window) | `rl:p:{id}:{minute}` | fail open |
| Idempotency store | `idem:{scope}:{player}:{key}` | fall through to database uniqueness guarantees |
| Job coordination locks | `lock:{job}` | jobs skip the tick |

Redis is **not** used for wallet updates or event standings: those need transactional guarantees
that a cache cannot give.

## Consequences
- Readiness depends on MySQL only; a Redis outage degrades features instead of taking the API
  out of the load balancer.
- Hit/miss/error counts are exported per cache so the effect of a Redis problem is visible in
  Grafana immediately.
