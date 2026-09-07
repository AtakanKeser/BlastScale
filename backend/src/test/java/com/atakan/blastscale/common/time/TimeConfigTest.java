package com.atakan.blastscale.common.time;

import org.junit.jupiter.api.Test;

import java.time.Clock;
import java.time.Instant;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * Pins the precision contract between the application clock and the DATETIME(6) columns: every
 * instant the application stamps must survive a MySQL round trip unchanged, so it can never be
 * rounded past the moment it was compared against (the live-event start window bug).
 */
class TimeConfigTest {

    @Test
    void applicationClockTicksInWholeMicroseconds() {
        Clock clock = new TimeConfig().clock();
        for (int i = 0; i < 1000; i++) {
            Instant now = Instant.now(clock);
            assertThat(now.getNano() % 1_000).as("nanosecond remainder of %s", now).isZero();
        }
    }
}
