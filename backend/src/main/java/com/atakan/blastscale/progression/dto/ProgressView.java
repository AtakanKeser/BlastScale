package com.atakan.blastscale.progression.dto;

import java.time.Instant;
import java.util.List;

public record ProgressView(int currentLevel, int totalStars, List<LevelEntry> levels) {

    public record LevelEntry(int level, int stars, int bestScore, int attempts, boolean cleared, Instant completedAt) {
    }
}
