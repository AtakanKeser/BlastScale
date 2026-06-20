package com.atakan.blastscale.level.dto;

import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

import java.util.List;
import java.util.Map;

public record UpsertLevelRequest(
        @Min(4) @Max(12) int rows,
        @Min(4) @Max(12) int cols,
        @Min(3) @Max(8) int colorCount,
        @Min(5) @Max(60) int moveLimit,
        @Min(100) int targetScore,
        @NotNull @Size(min = 3, max = 3) List<Integer> starThresholds,
        Map<String, Object> specialRules) {
}
