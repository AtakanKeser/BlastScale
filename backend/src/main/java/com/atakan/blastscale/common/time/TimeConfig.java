package com.atakan.blastscale.common.time;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import java.time.Clock;
import java.time.Duration;

/**
 * Exposes a single {@link Clock} bean.
 *
 * <p>All services read "now" from this clock instead of calling {@code Instant.now()} directly.
 * Tests can then replace the bean with a fixed or manually advanced clock to verify time based
 * logic (life regeneration, session expiry, daily rewards, leaderboard seasons) deterministically.
 */
@Configuration
public class TimeConfig {

    /**
     * Ticks in whole microseconds. MySQL stores DATETIME(6) with microsecond precision and
     * <b>rounds</b> finer values; with a nanosecond clock, "now" written to a row can come back a
     * microsecond later than the instant it was compared against, and a window check such as
     * {@code !now.isBefore(startAt)} fails for that microsecond. On macOS the JDK clock happens to
     * be microsecond-grained, on Linux it is not — which is how the live-event tests failed only in
     * CI. Aligning the clock with the storage precision removes the class of bug everywhere.
     */
    @Bean
    public Clock clock() {
        return Clock.tick(Clock.systemUTC(), Duration.ofNanos(1_000));
    }
}
