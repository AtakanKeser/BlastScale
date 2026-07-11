package com.atakan.blastscale.progression.dto;

import com.atakan.blastscale.economy.WalletSnapshot;
import com.atakan.blastscale.economy.reward.Reward;
import com.atakan.blastscale.event.dto.EventPointsAwarded;

import java.util.List;

/**
 * @param status      {@code COMPLETED} or {@code ALREADY_PROCESSED} (a retry of a session that was
 *                    already closed — no second reward)
 * @param nextLevel   the level the player may start next
 */
public record LevelCompleteResponse(
        String status,
        String sessionId,
        int level,
        int score,
        int stars,
        boolean firstClear,
        boolean newBestScore,
        Reward reward,
        WalletSnapshot wallet,
        int nextLevel,
        List<EventPointsAwarded> eventPoints) {

    public static final String COMPLETED = "COMPLETED";
    public static final String ALREADY_PROCESSED = "ALREADY_PROCESSED";
}
