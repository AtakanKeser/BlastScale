package com.atakan.blastscale.common.time;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import java.time.Clock;

/**
 * Exposes a single {@link Clock} bean.
 *
 * <p>All services read "now" from this clock instead of calling {@code Instant.now()} directly.
 * Tests can then replace the bean with a fixed or manually advanced clock to verify time based
 * logic (life regeneration, session expiry, daily rewards, leaderboard seasons) deterministically.
 */
@Configuration
public class TimeConfig {

    @Bean
    public Clock clock() {
        return Clock.systemUTC();
    }
}
