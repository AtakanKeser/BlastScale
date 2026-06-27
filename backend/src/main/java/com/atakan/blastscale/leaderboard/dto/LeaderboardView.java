package com.atakan.blastscale.leaderboard.dto;

import java.time.Instant;
import java.util.List;

/**
 * Response of {@code GET /api/v1/leaderboards/weekly}.
 *
 * @param myRank  1-based rank of the caller, or {@code null} when they have not scored this season
 */
public record LeaderboardView(
        String season,
        Instant endsAt,
        boolean finalized,
        List<Entry> players,
        Integer myRank,
        long myScore) {

    public record Entry(int rank, long playerId, String name, long score) {
    }
}
