package com.atakan.blastscale.leaderboard;

import org.junit.jupiter.api.Test;

import java.time.Instant;

import static org.assertj.core.api.Assertions.assertThat;

class LeaderboardSeasonTest {

    @Test
    void seasonIsTheIsoWeek() {
        assertThat(LeaderboardSeason.at(Instant.parse("2026-05-16T15:00:00Z"))).isEqualTo("2026-W20");
        assertThat(LeaderboardSeason.startOf("2026-W20")).isEqualTo(Instant.parse("2026-05-11T00:00:00Z"));
        assertThat(LeaderboardSeason.endOf("2026-W20")).isEqualTo(Instant.parse("2026-05-18T00:00:00Z"));
        assertThat(LeaderboardSeason.previous("2026-W20")).isEqualTo("2026-W19");
    }

    @Test
    void weekBoundariesAreMondayMidnightUtc() {
        assertThat(LeaderboardSeason.at(Instant.parse("2026-05-17T23:59:59Z"))).isEqualTo("2026-W20");
        assertThat(LeaderboardSeason.at(Instant.parse("2026-05-18T00:00:00Z"))).isEqualTo("2026-W21");
    }

    @Test
    void yearRolloverFollowsIsoRules() {
        // 2026 has 53 ISO weeks; Jan 1st 2027 (a Friday) still belongs to 2026-W53
        assertThat(LeaderboardSeason.at(Instant.parse("2027-01-01T12:00:00Z"))).isEqualTo("2026-W53");
        assertThat(LeaderboardSeason.previous("2027-W01")).isEqualTo("2026-W53");
        assertThat(LeaderboardSeason.isValid("2026-W20")).isTrue();
        assertThat(LeaderboardSeason.isValid("week36")).isFalse();
    }
}
