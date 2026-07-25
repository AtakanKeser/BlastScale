# ADR 0007: Exactly-once rewards

## Context
Mobile networks drop responses. The client retries `POST /levels/42/complete`; a naive server pays
twice. Two devices on one account, a crashed finalization job or an admin double-clicking
"finalize" create the same problem for leaderboard and event prizes.

## Decision
Three independent layers, any one of which is sufficient:

1. **Idempotency-Key** header stored in Redis (`IdempotencyService`): a retried request returns
   the stored response with `Idempotent-Replayed: true` and never executes twice.
2. **Conditional state transition**: `UPDATE game_session SET status = COMPLETED ... WHERE status =
   ACTIVE` succeeds for exactly one request; losers answer `ALREADY_PROCESSED` with the stored result.
3. **Ledger uniqueness**: `(player_id, reason, reference_id, resource)` is unique; the session id
   (or season / event id) is the reference, so the database itself refuses a second payment.

Season and event finalization additionally record a completion row and skip already-paid players,
so a partially failed run can be resumed safely.

## Consequences
- `ConcurrentRewardIntegrationTest` fires 100 simultaneous completions of one session and asserts a
  single ledger entry — with the same key and with different keys.
- Redis being unavailable degrades to layers 2 and 3; correctness does not depend on the cache.
