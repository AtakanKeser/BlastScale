package com.atakan.blastscale.integration;

import org.junit.jupiter.api.Test;
import org.springframework.test.context.TestPropertySource;

import java.time.Duration;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * Runs in its own Spring context (the differing property forces one) with a tiny limit so the Redis
 * counter can be exercised in a few requests. It deliberately does <b>not</b> redeclare
 * {@code @SpringBootTest}: the inherited annotation carries the base properties, so this context
 * keeps the same relaxed validators, cache TTLs and disabled background jobs as every other
 * integration test. Redeclaring it once made this context run with production defaults and, because
 * both contexts share the Redis container, it poisoned other tests' caches.
 */
@TestPropertySource(properties = "blastscale.rate-limit.requests-per-minute=5")
class RateLimitIntegrationTest extends AbstractIntegrationTest {

    @Test
    void exceedingTheLimitReturns429() {
        // The fixed window is keyed by minute of the application clock: move to a fresh window so
        // the anonymous (per IP) counter filled by the other test classes does not interfere.
        mutableClock().advance(Duration.ofMinutes(2));
        String token = api.register(uniqueUsername("limited"));
        int allowed = 0;
        ApiTestClient.Response last = null;
        for (int i = 0; i < 10; i++) {
            last = api.get("/api/v1/players/me", token);
            if (last.status() == 200) {
                allowed++;
            } else {
                break;
            }
        }
        assertThat(allowed).isLessThanOrEqualTo(5);
        assertThat(last.status()).isEqualTo(429);
        assertThat(last.text("code")).isEqualTo("RATE_LIMITED");
        assertThat(last.headers().getFirst("Retry-After")).isEqualTo("60");
    }
}
