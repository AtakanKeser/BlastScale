# ADR 0004: MongoDB for level and event configuration documents

## Context
A level definition is a nested document (board size, colours, move limit, objective, star
thresholds, free-form special rules) that designers version and tweak. It is never joined with
player state and its shape changes as the game evolves.

## Decision
Level definitions live in MongoDB (`levels` collection, id `level-{n}`). Player progression stays
in MySQL and references levels by number only.

Because MongoDB is not gameplay critical, the lookup has two fallbacks:
Redis cache -> MongoDB -> procedural generator (which also seeds MongoDB on first use). Driver
timeouts are short (2 s) so the fallback is fast.

## Consequences
- Designers can hand-tune a level from the admin panel without a schema migration.
- A MongoDB outage never blocks the game; players simply get generated levels until it returns.
- Live events also carry their rule configuration as JSON, but they are stored in MySQL because
  their lifecycle (scheduled/active/finalized) and prize payments are transactional.
