package com.atakan.blastscale.integration;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneId;
import java.time.ZoneOffset;
import java.time.temporal.ChronoUnit;

/**
 * A clock tests can move forward ("advance 24 hours, claim the daily reward again").
 * It starts at the real time and only ever moves forward, so JWTs issued by the application
 * stay valid for Spring Security's real-time expiry check.
 *
 * <p>Like the production clock ({@code TimeConfig}) it ticks in whole microseconds — the
 * precision of the DATETIME(6) columns. A sub-microsecond remainder is rounded up by MySQL, and
 * because this clock is frozen between advances the rounded value would stay "in the future" for
 * the rest of the test; that is exactly how the live-event tests failed on Linux CI while passing
 * on macOS, whose JDK clock is microsecond-grained.
 */
public class MutableClock extends Clock {

    private volatile Instant instant = Instant.now().truncatedTo(ChronoUnit.MICROS);
    private final ZoneId zone;

    public MutableClock() {
        this(ZoneOffset.UTC);
    }

    private MutableClock(ZoneId zone) {
        this.zone = zone;
    }

    public void advance(Duration duration) {
        instant = instant.plus(duration).truncatedTo(ChronoUnit.MICROS);
    }

    /** Back to real time (only if real time is later; never travel backwards). */
    public void reset() {
        Instant now = Instant.now().truncatedTo(ChronoUnit.MICROS);
        if (now.isAfter(instant)) {
            instant = now;
        }
    }

    @Override
    public ZoneId getZone() {
        return zone;
    }

    @Override
    public Clock withZone(ZoneId zone) {
        MutableClock copy = new MutableClock(zone);
        copy.instant = instant;
        return this.zone.equals(zone) ? this : copy;
    }

    @Override
    public Instant instant() {
        return instant;
    }
}
