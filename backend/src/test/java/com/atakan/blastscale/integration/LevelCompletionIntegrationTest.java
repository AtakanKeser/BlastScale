package com.atakan.blastscale.integration;

import com.atakan.blastscale.common.web.ApiHeaders;
import org.junit.jupiter.api.Test;
import tools.jackson.databind.JsonNode;

import java.time.Duration;
import java.util.HashMap;
import java.util.Map;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;

/** The happy path and the anti-cheat rejections of the level flow, end to end over HTTP. */
class LevelCompletionIntegrationTest extends AbstractIntegrationTest {

    @Test
    void completeLevelPaysRewardExactlyOnceAndAdvancesProgression() {
        String token = api.register(uniqueUsername("gamer"));
        long coinsBefore = api.get("/api/v1/economy/wallet", token).number("coins");

        ApiTestClient.Response start = api.startLevel(token, 1);
        assertThat(start.status()).isEqualTo(200);
        assertThat(start.integer("livesRemaining")).isEqualTo(4);
        assertThat(start.body().get("board").get("rows").asInt()).isEqualTo(8);

        Map<String, Object> body = api.solve(start.body());
        String key = UUID.randomUUID().toString();
        ApiTestClient.Response done = api.completeLevel(token, 1, body, key);
        assertThat(done.status()).isEqualTo(200);
        assertThat(done.text("status")).isEqualTo("COMPLETED");
        assertThat(done.headers().getFirst(ApiHeaders.IDEMPOTENT_REPLAYED)).isEqualTo("false");
        assertThat(done.body().get("firstClear").asBoolean()).isTrue();
        assertThat(done.integer("nextLevel")).isEqualTo(2);
        long reward = done.body().get("reward").get("coins").asLong();
        assertThat(reward).isPositive();
        assertThat(done.body().get("wallet").get("coins").asLong()).isEqualTo(coinsBefore + reward);
        assertThat(done.body().get("wallet").get("stars").asInt()).isEqualTo(done.integer("stars"));

        // same Idempotency-Key: identical response, flagged as a replay, nothing paid again
        ApiTestClient.Response replay = api.completeLevel(token, 1, body, key);
        assertThat(replay.status()).isEqualTo(200);
        assertThat(replay.headers().getFirst(ApiHeaders.IDEMPOTENT_REPLAYED)).isEqualTo("true");
        assertThat(replay.text("status")).isEqualTo("COMPLETED");
        assertThat(replay.body().get("wallet").get("coins").asLong()).isEqualTo(coinsBefore + reward);

        // new key, same session: the session is closed, so the stored result is returned
        ApiTestClient.Response again = api.completeLevel(token, 1, body, UUID.randomUUID().toString());
        assertThat(again.status()).isEqualTo(200);
        assertThat(again.text("status")).isEqualTo("ALREADY_PROCESSED");
        assertThat(api.get("/api/v1/economy/wallet", token).number("coins")).isEqualTo(coinsBefore + reward);

        // the ledger holds exactly one LEVEL_COMPLETE coin entry for the session
        JsonNode transactions = api.get("/api/v1/economy/transactions?size=50", token).body().get("content");
        long levelCompleteCoinRows = 0;
        for (JsonNode t : transactions) {
            if ("LEVEL_COMPLETE".equals(t.get("reason").asText()) && "COIN".equals(t.get("resource").asText())
                    && body.get("sessionId").equals(t.get("referenceId").asText())) {
                levelCompleteCoinRows++;
            }
        }
        assertThat(levelCompleteCoinRows).isEqualTo(1);

        JsonNode progress = api.get("/api/v1/progress", token).body();
        assertThat(progress.get("currentLevel").asInt()).isEqualTo(2);
        assertThat(progress.get("levels").get(0).get("cleared").asBoolean()).isTrue();
        assertThat(api.get("/api/v1/players/me", token).integer("currentLevel")).isEqualTo(2);
    }

    @Test
    void tamperedScoreAndMovesAreRejected() {
        String token = api.register(uniqueUsername("cheater"));
        ApiTestClient.Response start = api.startLevel(token, 1);
        Map<String, Object> honest = api.solve(start.body());

        Map<String, Object> inflated = new HashMap<>(honest);
        inflated.put("score", (int) honest.get("score") + 10);
        ApiTestClient.Response r1 = api.completeLevel(token, 1, inflated, null);
        assertThat(r1.status()).isEqualTo(422);
        assertThat(r1.text("code")).isEqualTo("SCORE_MISMATCH");
        assertThat(r1.body().get("details").get("serverScore").asInt()).isEqualTo(honest.get("score"));

        Map<String, Object> impossible = new HashMap<>(honest);
        impossible.put("score", 99_999_999);
        assertThat(api.completeLevel(token, 1, impossible, null).text("code")).isEqualTo("SCORE_OUT_OF_RANGE");

        Map<String, Object> noMoves = new HashMap<>(honest);
        noMoves.put("moves", java.util.List.of());
        noMoves.put("score", 0);
        noMoves.put("movesUsed", 0);
        assertThat(api.completeLevel(token, 1, noMoves, null).text("code")).isEqualTo("OBJECTIVE_NOT_REACHED");

        // the honest result still goes through afterwards: rejections do not burn the session
        ApiTestClient.Response ok = api.completeLevel(token, 1, honest, null);
        assertThat(ok.status()).isEqualTo(200);
        assertThat(ok.text("status")).isEqualTo("COMPLETED");
    }

    @Test
    void lockedLevelsAndForeignSessionsAreRefused() {
        String alice = api.register(uniqueUsername("alice"));
        String bob = api.register(uniqueUsername("bob"));

        ApiTestClient.Response locked = api.startLevel(alice, 5);
        assertThat(locked.status()).isEqualTo(403);
        assertThat(locked.text("code")).isEqualTo("LEVEL_LOCKED");

        ApiTestClient.Response start = api.startLevel(alice, 1);
        Map<String, Object> body = api.solve(start.body());
        ApiTestClient.Response stolen = api.completeLevel(bob, 1, body, null);
        assertThat(stolen.status()).isEqualTo(404);
        assertThat(stolen.text("code")).isEqualTo("SESSION_NOT_FOUND");
    }

    @Test
    void livesAreConsumedAndRegenerateWithTime() {
        String token = api.register(uniqueUsername("lives"));
        for (int i = 0; i < 5; i++) {
            ApiTestClient.Response start = api.startLevel(token, 1);
            assertThat(start.status()).as("start #%d", i).isEqualTo(200);
            assertThat(start.integer("livesRemaining")).isEqualTo(4 - i);
        }
        ApiTestClient.Response noLives = api.startLevel(token, 1);
        assertThat(noLives.status()).isEqualTo(409);
        assertThat(noLives.text("code")).isEqualTo("NO_LIVES_LEFT");
        assertThat(noLives.body().get("details").get("nextLifeInSeconds").asLong()).isBetween(1L, 1800L);

        mutableClock().advance(Duration.ofMinutes(31)); // one regeneration interval
        ApiTestClient.Response afterRegen = api.get("/api/v1/economy/wallet", token);
        assertThat(afterRegen.integer("lives")).isEqualTo(1);
        assertThat(api.startLevel(token, 1).status()).isEqualTo(200);
    }

    @Test
    void failingALevelKeepsTheLifeSpentAndClosesTheSession() {
        String token = api.register(uniqueUsername("loser"));
        ApiTestClient.Response start = api.startLevel(token, 1);
        String sessionId = start.text("sessionId");
        ApiTestClient.Response failed = api.post("/api/v1/levels/1/fail",
                Map.of("sessionId", sessionId, "moves", java.util.List.of(), "extraMovesUsed", false), token);
        assertThat(failed.status()).isEqualTo(200);
        assertThat(failed.text("status")).isEqualTo("FAILED");
        assertThat(failed.body().get("wallet").get("lives").asInt()).isEqualTo(4);

        // a completion for the failed session is refused
        Map<String, Object> body = api.solve(start.body());
        ApiTestClient.Response late = api.completeLevel(token, 1, body, null);
        assertThat(late.status()).isEqualTo(409);
        assertThat(late.text("code")).isEqualTo("SESSION_NOT_ACTIVE");
    }
}
