# ADR 0002: MySQL with an append-only ledger is the source of truth

## Context
Coins, lives and boosters are real value to players. "player.coins += 100" hides history, makes
duplicate rewards invisible and cannot be audited when a support ticket arrives.

## Decision
- `player_wallet` holds current balances; `economy_transaction` is an append-only ledger with
  `type`, `resource`, `amount`, `balance_after`, `reason`, `reference_id`.
- Every balance change goes through `EconomyService.apply` inside one transaction:
  `SELECT ... FOR UPDATE` on the wallet row, duplicate-reference check, balance checks,
  ledger insert, wallet update (`@Version` optimistic guard on top), outbox event.
- The unique key `(player_id, reason, reference_id, resource)` makes any reward exactly-once at
  the database level.
- MySQL (InnoDB, READ COMMITTED semantics with row locks) was chosen over PostgreSQL because it is
  the most common choice for user-facing game backends and its locking behaviour is simple to
  reason about for this access pattern (hot rows per player, no cross-player contention).

## Consequences
- Support can answer "why do I have 650 coins?" from the ledger alone; fraud analysis is a query.
- Balances are always reconcilable: `SUM(amount) == balance` (covered by an integration test).
- Writes serialise per player (a player rarely issues concurrent writes), never globally.
