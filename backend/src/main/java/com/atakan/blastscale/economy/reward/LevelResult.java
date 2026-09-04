package com.atakan.blastscale.economy.reward;

/** Validated outcome of a level attempt (produced by the progression module's validation chain). */
public record LevelResult(int levelId, int score, int stars, int movesUsed, int moveLimit, boolean firstClear) {
}
