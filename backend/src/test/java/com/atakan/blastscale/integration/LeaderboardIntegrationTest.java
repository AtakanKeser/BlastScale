package com.atakan.blastscale.integration;

import org.junit.jupiter.api.Test;
import tools.jackson.databind.JsonNode;

import java.time.Duration;
import java.util.concurrent.ThreadLocalRandom;

import static org.assertj.core.api.Assertions.assertThat;

class LeaderboardIntegrationTest extends AbstractIntegrationTest {

    @Test
    void scoresRankAndPrizesArePaidOnce() {
        // jump to a fresh, never used season so other tests' scores do not interfere
        mutableClock().advance(Duration.ofDays(7L * (100 + ThreadLocalRandom.current().nextInt(500))));

        String alice = api.register(uniqueUsername("lb_alice"));
        String bob = api.register(uniqueUsername("lb_bob"));
        long aliceId = api.playerId(alice);
        long bobId = api.playerId(bob);
        int aliceScore = api.playAndWin(alice, 1).integer("score");
        int bobScore1 = api.playAndWin(bob, 1).integer("score");
        int bobScore2 = api.playAndWin(bob, 2).integer("score"); // bob plays twice: scores add up

        JsonNode board = api.get("/api/v1/leaderboards/weekly?limit=10", alice).body();
        assertThat(board.get("season").asText()).matches("\\d{4}-W\\d{2}");
        assertThat(board.get("finalized").asBoolean()).isFalse();
        JsonNode players = board.get("players");
        assertThat(players.size()).isEqualTo(2);
        long expectedBob = bobScore1 + bobScore2;
        boolean bobFirst = expectedBob > aliceScore;
        assertThat(players.get(0).get("playerId").asLong()).isEqualTo(bobFirst ? bobId : aliceId);
        assertThat(players.get(0).get("score").asLong()).isEqualTo(bobFirst ? expectedBob : aliceScore);
        assertThat(board.get("myRank").asInt()).isEqualTo(bobFirst ? 2 : 1);
        assertThat(board.get("myScore").asLong()).isEqualTo(aliceScore);

        String admin = api.adminToken();
        String season = board.get("season").asText();
        long aliceCoins = api.get("/api/v1/economy/wallet", alice).number("coins");

        ApiTestClient.Response notEnded = api.post("/api/v1/admin/leaderboards/" + season + "/finalize", null, admin);
        assertThat(notEnded.status()).isEqualTo(409);
        assertThat(notEnded.text("code")).isEqualTo("LEADERBOARD_SEASON_ACTIVE");

        ApiTestClient.Response finalized = api.post("/api/v1/admin/leaderboards/" + season + "/finalize?force=true", null, admin);
        assertThat(finalized.status()).isEqualTo(200);
        assertThat(finalized.body().get("alreadyFinalized").asBoolean()).isFalse();
        assertThat(finalized.body().get("rewards").size()).isEqualTo(2);
        int alicePrize = bobFirst ? 3000 : 5000;
        assertThat(api.get("/api/v1/economy/wallet", alice).number("coins")).isEqualTo(aliceCoins + alicePrize);

        // running it again (crashed job, second replica, double click) pays nothing more
        ApiTestClient.Response again = api.post("/api/v1/admin/leaderboards/" + season + "/finalize?force=true", null, admin);
        assertThat(again.body().get("alreadyFinalized").asBoolean()).isTrue();
        assertThat(api.get("/api/v1/economy/wallet", alice).number("coins")).isEqualTo(aliceCoins + alicePrize);
        assertThat(api.get("/api/v1/leaderboards/weekly", alice).body().get("finalized").asBoolean()).isTrue();
    }
}
