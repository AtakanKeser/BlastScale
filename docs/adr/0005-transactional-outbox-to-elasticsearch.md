# ADR 0005: Telemetry through a transactional outbox into Elasticsearch

## Context
Investigating "I finished level 412 but got no reward" needs the exact sequence of events for a
player. Writing to Elasticsearch directly from the request path would either slow gameplay down or
lose events when Elasticsearch is unavailable, and could record events for transactions that were
rolled back.

## Decision
- Services write an `outbox_event` row in the same MySQL transaction as the business change.
- `OutboxPublisherJob` claims batches with `FOR UPDATE SKIP LOCKED` (several replicas can drain
  concurrently), bulk-indexes them into the `blastscale-events` index and marks them published;
  failures are retried with a bounded attempt count.
- Elasticsearch document ids equal outbox ids, so a redelivery after a crash is idempotent.
- The index is created lazily by the publisher; Elasticsearch is not part of readiness.

## Consequences
- Telemetry can never disagree with the database (no event without a commit, no commit without
  its event).
- Elasticsearch downtime only delays investigation data; the backlog is visible as
  `blastscale_outbox_pending` in Grafana.
- Latency cost on the request path is one cheap insert.
