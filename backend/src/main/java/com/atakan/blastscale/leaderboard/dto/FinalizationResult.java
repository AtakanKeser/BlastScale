package com.atakan.blastscale.leaderboard.dto;

import java.time.Instant;
import java.util.List;

public record FinalizationResult(String season, boolean alreadyFinalized, Instant finalizedAt, int participants,
                                 List<RewardedPlayer> rewards) {

    public record RewardedPlayer(int rank, long playerId, long score, int coins) {
    }
}
