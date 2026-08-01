package com.atakan.blastscale.integration;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;
import tools.jackson.databind.JsonNode;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;

import static org.assertj.core.api.Assertions.assertThat;

/** Live events change rewards and rankings at runtime, without a deploy. */
class LiveEventIntegrationTest extends AbstractIntegrationTest {

    private final List<Long> createdEvents = new ArrayList<>();
    private String admin;

    @AfterEach
    void cleanUpEvents() {
        // never leave an active event behind: it would change rewards for the other tests
        for (Long id : createdEvents) {
            api.post("/api/v1/admin/events/" + id + "/cancel", null, admin);
        }
    }

    @Test
    void doubleRewardEventMultipliesLevelRewards() {
        admin = api.adminToken();
        String token = api.register(uniqueUsername("event_player"));
        int standard = api.playAndWin(token, 1).body().get("reward").get("coins").asInt();

        ApiTestClient.Response created = api.post("/api/v1/admin/events", Map.of(
                "type", "DOUBLE_REWARD", "name", "Double Reward Weekend",
                "endAt", "2099-01-01T00:00:00Z", "configuration", Map.of("multiplier", 2.0)), admin);
        assertThat(created.status()).isEqualTo(201);
        assertThat(created.text("status")).isEqualTo("ACTIVE");
        createdEvents.add(created.number("id"));

        JsonNode doubled = api.playAndWin(token, 2).body();
        assertThat(doubled.get("reward").get("strategy").asText()).isEqualTo("DOUBLE_REWARD_EVENT");
        assertThat(doubled.get("reward").get("multiplier").asDouble()).isEqualTo(2.0);
        // the second clear has no first-clear bonus for the *same* level, but level 2 is also a first clear
        assertThat(doubled.get("reward").get("coins").asInt()).isGreaterThan(standard);
        assertThat(doubled.get("reward").get("coins").asInt() % 2).isZero();
    }

    @Test
    void rocketRaceAwardsPointsRanksPlayersAndPaysPrizesOnce() {
        admin = api.adminToken();
        ApiTestClient.Response race = api.post("/api/v1/admin/events", Map.of(
                "type", "ROCKET_RACE", "name", "Rocket Race", "endAt", "2099-01-01T00:00:00Z",
                "configuration", Map.of("pointsPerLevel", 1, "minimumLevel", 1, "rewards", Map.of("1", 1000, "2", 500))), admin);
        assertThat(race.status()).isEqualTo(201);
        long raceId = race.number("id");
        createdEvents.add(raceId);

        String fast = api.register(uniqueUsername("rocket_fast"));
        String slow = api.register(uniqueUsername("rocket_slow"));
        JsonNode first = api.playAndWin(fast, 1).body();
        assertThat(first.get("eventPoints").size()).isEqualTo(1);
        assertThat(first.get("eventPoints").get(0).get("points").asInt()).isEqualTo(1);
        api.playAndWin(fast, 2);
        api.playAndWin(slow, 1);

        JsonNode events = api.get("/api/v1/events", slow).body();
        JsonNode view = null;
        for (JsonNode e : events) {
            if (e.get("id").asLong() == raceId) {
                view = e;
            }
        }
        assertThat(view).isNotNull();
        assertThat(view.get("myPoints").asLong()).isEqualTo(1);
        assertThat(view.get("myRank").asInt()).isEqualTo(2);
        assertThat(view.get("top").get(0).get("points").asLong()).isEqualTo(2);

        long fastCoins = api.get("/api/v1/economy/wallet", fast).number("coins");
        long slowCoins = api.get("/api/v1/economy/wallet", slow).number("coins");
        ApiTestClient.Response ended = api.post("/api/v1/admin/events/" + raceId + "/end", null, admin);
        assertThat(ended.status()).isEqualTo(200);
        assertThat(ended.text("status")).isEqualTo("FINALIZED");
        assertThat(api.get("/api/v1/economy/wallet", fast).number("coins")).isEqualTo(fastCoins + 1000);
        assertThat(api.get("/api/v1/economy/wallet", slow).number("coins")).isEqualTo(slowCoins + 500);

        ApiTestClient.Response endAgain = api.post("/api/v1/admin/events/" + raceId + "/end", null, admin);
        assertThat(endAgain.status()).isEqualTo(409);
        assertThat(endAgain.text("code")).isEqualTo("EVENT_INVALID_STATE");
        assertThat(api.get("/api/v1/economy/wallet", fast).number("coins")).isEqualTo(fastCoins + 1000);
        createdEvents.clear(); // already finalized, nothing to cancel
    }
}
