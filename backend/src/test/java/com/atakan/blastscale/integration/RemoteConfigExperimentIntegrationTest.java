package com.atakan.blastscale.integration;

import org.junit.jupiter.api.Test;
import tools.jackson.databind.JsonNode;

import java.util.HashMap;
import java.util.List;
import java.util.Map;

import static org.assertj.core.api.Assertions.assertThat;

/** Remote config changes propagate without a deploy; experiments assign players deterministically. */
class RemoteConfigExperimentIntegrationTest extends AbstractIntegrationTest {

    @Test
    void configUpdatesAreVisibleImmediately() {
        String admin = api.adminToken();
        String token = api.register(uniqueUsername("cfg"));
        int original = api.get("/api/v1/config", token).body().get("config").get("dailyRewardCoins").asInt();
        try {
            ApiTestClient.Response updated = api.put("/api/v1/admin/config/dailyRewardCoins", Map.of("value", original + 50), admin);
            assertThat(updated.status()).isEqualTo(200);
            assertThat(api.get("/api/v1/config", token).body().get("config").get("dailyRewardCoins").asInt()).isEqualTo(original + 50);
            assertThat(api.get("/api/v1/admin/config", admin).status()).isEqualTo(200);
        } finally {
            api.put("/api/v1/admin/config/dailyRewardCoins", Map.of("value", original), admin);
        }
    }

    @Test
    void experimentAssignsPlayersStickilyAndOverridesTheirConfig() {
        String admin = api.adminToken();
        String key = "life_timer_" + uniqueUsername("x").replace("_", "");
        ApiTestClient.Response created = api.post("/api/v1/admin/experiments", Map.of(
                "key", key, "name", "Life timer experiment",
                "variants", List.of(
                        Map.of("name", "A", "weight", 50, "overrides", Map.of("lifeRegenerationMinutes", 30)),
                        Map.of("name", "B", "weight", 50, "overrides", Map.of("lifeRegenerationMinutes", 25)))), admin);
        assertThat(created.status()).isEqualTo(201);
        long experimentId = created.number("id");
        assertThat(api.post("/api/v1/admin/experiments/" + experimentId + "/start", null, admin).text("status")).isEqualTo("RUNNING");

        Map<String, String> variants = new HashMap<>();
        for (int i = 0; i < 16; i++) {
            String token = api.register(uniqueUsername("ab"));
            JsonNode config = api.get("/api/v1/config", token).body();
            JsonNode assignment = null;
            for (JsonNode e : config.get("experiments")) {
                if (e.get("key").asText().equals(key)) {
                    assignment = e;
                }
            }
            assertThat(assignment).as("player %d is assigned", i).isNotNull();
            String variant = assignment.get("variant").asText();
            int expectedMinutes = variant.equals("A") ? 30 : 25;
            assertThat(config.get("config").get("lifeRegenerationMinutes").asInt()).isEqualTo(expectedMinutes);
            // sticky: the second read returns the same variant
            JsonNode again = api.get("/api/v1/config", token).body();
            assertThat(again.get("experiments").get(0).get("variant").asText()).isEqualTo(variant);
            variants.put(token, variant);
        }
        assertThat(variants.values()).contains("A", "B"); // both arms received traffic

        JsonNode view = api.get("/api/v1/admin/experiments/" + experimentId, admin).body();
        long counted = view.get("assignments").get("A").asLong() + view.get("assignments").get("B").asLong();
        assertThat(counted).isEqualTo(16);

        assertThat(api.post("/api/v1/admin/experiments/" + experimentId + "/end", null, admin).text("status")).isEqualTo("ENDED");
        String anyToken = variants.keySet().iterator().next();
        assertThat(api.get("/api/v1/config", anyToken).body().get("experiments").size()).isZero();
        assertThat(api.get("/api/v1/config", anyToken).body().get("config").get("lifeRegenerationMinutes").asInt()).isEqualTo(30);
    }
}
