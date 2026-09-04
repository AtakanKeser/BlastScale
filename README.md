# BlastScale

BlastScale is a production-oriented backend platform for a casual mobile puzzle game, designed to
explore the engineering challenges that emerge when gameplay systems scale to large player
populations.

> Work in progress — this README grows with the project. See the commit history for the order in
> which the modules were built.

## Stack

| Layer | Technology |
|-------|------------|
| API | Java 21, Spring Boot, Spring Web MVC, Spring Security (JWT) |
| Transactional state | MySQL 8.4, Spring Data JPA, Flyway |
| Low latency / ephemeral state | Redis 7 |
| Flexible configuration documents | MongoDB 8 |
| Telemetry & investigation | Elasticsearch 9 |
| Observability | Actuator, Micrometer, Prometheus, Grafana |
| Tests | JUnit, Mockito, Testcontainers, k6 |
| Client | Unity (C#) |
| Infrastructure | Docker Compose, GitHub Actions |

## Running

```bash
docker compose up --build
```
