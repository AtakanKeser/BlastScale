# Demo script (about three minutes)

A walkthrough that shows the backend doing the things a real live game backend has to do.
Everything below runs against `docker compose up --build`.

## 1. Play a level in the Unity client

Open the Unity project, press Play, tap **Continue as guest**. The home screen shows
level 1, 500 coins, 5 lives. Play the level; when the target is reached press **Finish**.

The result screen shows the reward (for example **+125 coins**, strategy `STANDARD`). The client
only sent its *moves*; the server replayed them and computed the score itself.

## 2. Look at the ledger

Admin panel → Players → the guest player → **Ledger**:

```
CREDIT COIN  +125  LEVEL_COMPLETE  <session id>
DEBIT  LIFE  -1    LEVEL_START     <session id>
CREDIT LIFE  +5    INITIAL_GRANT   player:<id>
CREDIT COIN  +500  INITIAL_GRANT   player:<id>
```

## 3. Replay the same completion (idempotency)

Take the completion request the client sent (visible in the Unity console) and send it again with
the same `Idempotency-Key` from a terminal:

```bash
curl -i -X POST localhost:8080/api/v1/levels/1/complete \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -H "Idempotency-Key: <same key>" -d @completion.json
```

Response: same body, header `Idempotent-Replayed: true`. Send it with a *new* key:
`"status": "ALREADY_PROCESSED"`. The wallet still shows 625 coins; the ledger still has one
`LEVEL_COMPLETE` row.

## 4. Leaderboard

`GET /api/v1/leaderboards/weekly` — the player appears with rank and score; the data comes from
the Redis sorted set `lb:weekly:<season>` (`redis-cli ZREVRANGE lb:weekly:2026-W36 0 9 WITHSCORES`).

## 5. Start a live event without deploying anything

Admin panel → Events → **Create**: type `DOUBLE_REWARD`, name "Double Reward Weekend", end date
tomorrow, configuration `{"multiplier": 2}`. Back in the Unity client, play level 2: the reward is
doubled and the strategy reads `DOUBLE_REWARD_EVENT`. No client update, no server restart.

Create a `ROCKET_RACE` the same way; every completed level now adds a rocket and the Events screen
shows the player's rank. **End** the event from the panel: the top players receive their prizes
in the ledger (`EVENT_REWARD`, reference `event:<id>`), and ending it again is refused.

## 6. Run an A/B experiment

Admin panel → Experiments → create `life_timer_v2` with variants A (`lifeRegenerationMinutes: 30`,
50 %) and B (`lifeRegenerationMinutes: 25`, 50 %) → **Start**. `GET /api/v1/config` for the player
now contains `"experiments": [{"key": "life_timer_v2", "variant": "B"}]` and
`"lifeRegenerationMinutes": 25`. The same player always gets the same variant; the panel shows how
many players landed in each arm.

## 7. Investigate a player

Admin panel → Players → player → **Events**: the Elasticsearch timeline shows
`LEVEL_STARTED`, `LEVEL_COMPLETED`, `ECONOMY_TRANSACTION`, `EXPERIMENT_ASSIGNED`, … with payloads.
Filter by `COMPLETION_REJECTED` to see attempts the anti-cheat chain refused, and which validator
refused them.

## 8. Load and dashboards

```bash
docker compose --profile loadtest run --rm k6 run -e VUS=200 -e DURATION=2m /scripts/scenario.js
```

Open Grafana (http://localhost:3000, dashboard "BlastScale Overview"): requests/sec, p95/p99,
error rate, level completions per minute by result, reward pipeline latency, cache hit rate, outbox
backlog, connection pool. Stop Elasticsearch (`docker compose stop elasticsearch`) during the run:
gameplay continues, the outbox backlog grows, and it drains when Elasticsearch is started again.
