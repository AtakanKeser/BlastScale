package com.atakan.blastscale.integration;

import org.junit.jupiter.api.Test;
import org.springframework.web.client.RestClient;

import static org.assertj.core.api.Assertions.assertThat;

class HealthAndMetricsIntegrationTest extends AbstractIntegrationTest {

    @Test
    void probesAndPrometheusAreExposed() {
        ApiTestClient.Response readiness = api.get("/actuator/health/readiness", null);
        assertThat(readiness.status()).isEqualTo(200);
        assertThat(readiness.text("status")).isEqualTo("UP");
        assertThat(api.get("/actuator/health/liveness", null).text("status")).isEqualTo("UP");
        ApiTestClient.Response health = api.get("/actuator/health", null);
        assertThat(health.body().get("components").has("db")).isTrue();
        assertThat(health.body().get("components").has("redis")).isTrue();

        String token = api.register(uniqueUsername("metrics"));
        api.playAndWin(token, 1);
        String scrape = RestClient.create().get().uri("http://localhost:" + port + "/actuator/prometheus").retrieve().body(String.class);
        assertThat(scrape).contains("blastscale_level_completion_total")
                .contains("blastscale_economy_transaction_total")
                .contains("blastscale_cache_requests_total")
                .contains("http_server_requests_seconds_bucket");
    }

    @Test
    void protectedEndpointsRequireAValidToken() {
        assertThat(api.get("/api/v1/players/me", null).status()).isEqualTo(401);
        assertThat(api.get("/api/v1/players/me", "not-a-jwt").status()).isEqualTo(401);
        String player = api.register(uniqueUsername("nonadmin"));
        ApiTestClient.Response forbidden = api.get("/api/v1/admin/players", player);
        assertThat(forbidden.status()).isEqualTo(403);
        assertThat(forbidden.text("code")).isEqualTo("FORBIDDEN");
    }
}
