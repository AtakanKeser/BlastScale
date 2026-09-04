package com.atakan.blastscale.integration;

import org.awaitility.Awaitility;
import org.junit.jupiter.api.Test;
import tools.jackson.databind.JsonNode;

import java.time.Duration;
import java.util.HashSet;
import java.util.Set;

import static org.assertj.core.api.Assertions.assertThat;

/** Events written in the gameplay transaction reach Elasticsearch through the outbox worker. */
class OutboxTelemetryIntegrationTest extends AbstractIntegrationTest {

    @Test
    void gameplayEventsAreSearchablePerPlayer() {
        String admin = api.adminToken();
        String token = api.register(uniqueUsername("telemetry"));
        long playerId = api.playerId(token);
        api.playAndWin(token, 1);

        Awaitility.await().atMost(Duration.ofSeconds(30)).pollInterval(Duration.ofMillis(500)).untilAsserted(() -> {
            ApiTestClient.Response page = api.get("/api/v1/admin/players/" + playerId + "/events?size=100", admin);
            assertThat(page.status()).isEqualTo(200);
            Set<String> types = new HashSet<>();
            for (JsonNode event : page.body().get("events")) {
                types.add(event.get("eventType").asText());
            }
            assertThat(types).contains("PLAYER_REGISTERED", "LEVEL_STARTED", "LEVEL_COMPLETED", "ECONOMY_TRANSACTION");
        });

        ApiTestClient.Response filtered = api.get("/api/v1/admin/players/" + playerId + "/events?type=LEVEL_COMPLETED", admin);
        assertThat(filtered.body().get("events").size()).isEqualTo(1);
        JsonNode completed = filtered.body().get("events").get(0);
        assertThat(completed.get("payload").get("level").asInt()).isEqualTo(1);
        assertThat(completed.get("payload").get("rewardCoins").asLong()).isPositive();

        Awaitility.await().atMost(Duration.ofSeconds(15)).untilAsserted(() ->
                assertThat(api.get("/api/v1/admin/telemetry/outbox", admin).number("pending")).isZero());
    }
}
