package com.atakan.blastscale.economy.dto;

import com.atakan.blastscale.economy.WalletSnapshot;

import java.time.Instant;

public record DailyRewardResult(int coins, int streak, Instant nextClaimAt, WalletSnapshot wallet) {
}
