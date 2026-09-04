# Architecture Decision Records

Short records of the decisions that shape BlastScale, in the order they were made.
Each one states the context, the decision and the consequences we accept.

| # | Decision |
|---|----------|
| [0001](0001-modular-monolith.md) | Start as a modular monolith, not microservices |
| [0002](0002-mysql-ledger-as-source-of-truth.md) | MySQL with an append-only ledger is the source of truth for player state |
| [0003](0003-redis-for-ephemeral-state.md) | Redis only for problems that need it: cache, leaderboard, rate limit, idempotency, locks |
| [0004](0004-mongodb-for-level-configuration.md) | MongoDB for level and event configuration documents |
| [0005](0005-transactional-outbox-to-elasticsearch.md) | Telemetry through a transactional outbox into Elasticsearch |
| [0006](0006-server-authoritative-gameplay.md) | The server replays moves; the client never decides the outcome |
| [0007](0007-exactly-once-rewards.md) | Exactly-once rewards via idempotency keys, conditional updates and unique ledger keys |
