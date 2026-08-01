package com.atakan.blastscale.integration;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneId;
import java.time.ZoneOffset;

/**
 * A clock tests can move forward ("advance 24 hours, claim the daily reward again").
 * It starts at the real time and only ever moves forward, so JWTs issued by the application
 * stay valid for Spring Security's real-time expiry check.
 */
public class MutableClock extends Clock {

    private volatile Instant instant = Instant.now();
    private final ZoneId zone;

    public MutableClock() {
        this(ZoneOffset.UTC);
    }

    private MutableClock(ZoneId zone) {
        this.zone = zone;
    }

    public void advance(Duration duration) {
        instant = instant.plus(duration);
    }

    /** Back to real time (only if real time is later; never travel backwards). */
    public void reset() {
        Instant now = Instant.now();
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
