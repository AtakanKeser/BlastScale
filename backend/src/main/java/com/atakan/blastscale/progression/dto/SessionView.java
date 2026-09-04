package com.atakan.blastscale.progression.dto;

import com.atakan.blastscale.progression.GameSession;

import java.time.Instant;

public record SessionView(String id, int level, int seed, String status, Instant startedAt, Instant completedAt,
                          Integer score, Integer movesUsed, Integer stars, Long rewardCoins, String rewardStrategy) {

    public static SessionView from(GameSession s) {
        return new SessionView(s.getId(), s.getLevelId(), s.getSeed(), s.getStatus().name(), s.getStartedAt(),
                s.getCompletedAt(), s.getScore(), s.getMovesUsed(), s.getStars(), s.getRewardCoins(), s.getRewardStrategy());
    }
}
