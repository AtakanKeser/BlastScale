package com.atakan.blastscale.progression.dto;

import com.atakan.blastscale.level.engine.Move;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

import java.util.List;

/**
 * The client reports its result <b>and the moves that produced it</b>. The server replays the
 * moves; {@code score} and {@code movesUsed} are only cross-checked, never trusted.
 */
public record LevelCompleteRequest(
        @NotBlank String sessionId,
        @Min(0) int score,
        @Min(0) int movesUsed,
        @NotNull @Size(max = 400) List<@Valid Move> moves,
        boolean extraMovesUsed) {
}
