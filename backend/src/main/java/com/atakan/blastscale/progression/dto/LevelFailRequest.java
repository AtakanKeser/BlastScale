package com.atakan.blastscale.progression.dto;

import com.atakan.blastscale.level.engine.Move;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

import java.util.List;

/** A lost level still needs the moves so used boosters can be charged. */
public record LevelFailRequest(
        @NotBlank String sessionId,
        @NotNull @Size(max = 400) List<@Valid Move> moves,
        boolean extraMovesUsed) {
}
