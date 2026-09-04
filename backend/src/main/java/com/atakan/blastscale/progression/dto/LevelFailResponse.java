package com.atakan.blastscale.progression.dto;

import com.atakan.blastscale.economy.WalletSnapshot;

public record LevelFailResponse(String status, String sessionId, int level, int score, WalletSnapshot wallet) {
}
