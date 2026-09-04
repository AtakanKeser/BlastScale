package com.atakan.blastscale.economy;

import org.junit.jupiter.api.Test;

import java.time.Duration;
import java.time.Instant;

import static org.assertj.core.api.Assertions.assertThat;

class LifeRegenerationTest {

    private static final Instant T0 = Instant.parse("2026-09-04T10:00:00Z");

    @Test
    void fullLivesNeverRegenerateAndTimerFollowsNow() {
        LifeRegeneration.Result result = LifeRegeneration.apply(5, T0, T0.plus(Duration.ofHours(3)), 5, 30);
        assertThat(result.lives()).isEqualTo(5);
        assertThat(result.nextLifeInSeconds()).isZero();
        assertThat(result.livesUpdatedAt()).isEqualTo(T0.plus(Duration.ofHours(3)));
    }

    @Test
    void regeneratesWholeIntervalsAndKeepsPartialProgress() {
        // 65 minutes at 30 min/life: +2 lives, 5 minutes of progress towards the next one
        LifeRegeneration.Result result = LifeRegeneration.apply(1, T0, T0.plus(Duration.ofMinutes(65)), 5, 30);
        assertThat(result.lives()).isEqualTo(3);
        assertThat(result.livesUpdatedAt()).isEqualTo(T0.plus(Duration.ofMinutes(60)));
        assertThat(result.nextLifeInSeconds()).isEqualTo(25 * 60);
    }

    @Test
    void capsAtMaxAndResetsTimer() {
        Instant now = T0.plus(Duration.ofHours(10));
        LifeRegeneration.Result result = LifeRegeneration.apply(0, T0, now, 5, 30);
        assertThat(result.lives()).isEqualTo(5);
        assertThat(result.nextLifeInSeconds()).isZero();
        assertThat(result.livesUpdatedAt()).isEqualTo(now);
    }

    @Test
    void clockSkewNeverRegeneratesNegativeTime() {
        LifeRegeneration.Result result = LifeRegeneration.apply(2, T0, T0.minus(Duration.ofMinutes(5)), 5, 30);
        assertThat(result.lives()).isEqualTo(2);
        assertThat(result.livesUpdatedAt()).isEqualTo(T0);
    }

    @Test
    void experimentCanShortenTheInterval() {
        assertThat(LifeRegeneration.apply(0, T0, T0.plus(Duration.ofMinutes(50)), 5, 30).lives()).isEqualTo(1);
        assertThat(LifeRegeneration.apply(0, T0, T0.plus(Duration.ofMinutes(50)), 5, 25).lives()).isEqualTo(2);
    }
}
