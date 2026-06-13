package com.atakan.blastscale.economy.dto;

import java.time.Instant;

public record DailyRewardStatus(boolean available, int currentStreak, int nextRewardCoins, Instant nextClaimAt) {
}
