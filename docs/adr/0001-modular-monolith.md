# ADR 0001: Start as a modular monolith

## Context
The platform has eight domains (player, economy, progression, level, leaderboard, event,
experiment, telemetry). A common reflex is to make each one a microservice from day one.

## Decision
I intentionally chose a modular monolith because the current domain does not justify the
operational complexity of microservices. Modules have explicit boundaries and can be extracted
independently if traffic or ownership requirements change.

Concretely:
- one deployable, one database transaction spanning progression + economy + telemetry
  (which is exactly what makes exactly-once rewards simple);
- each module is a package with its own controllers, services and repositories; modules talk
  only through service classes or Spring application events (`PlayerRegisteredEvent`,
  `WalletChangedEvent`), never through each other's repositories;
- the application is stateless, so scaling is `docker compose up --scale api=3` behind nginx.

## Consequences
- Far less infrastructure: no service mesh, no distributed transactions, no saga for a level
  completion.
- Extraction path is clear: a module such as `leaderboard` only needs its Redis key space and
  the `PlayerService.usernamesOf` lookup to become a service.
- The trade-off is a single build and deploy unit; acceptable at this scale.
