package com.atakan.blastscale.level.engine;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;

/** One recorded player action. Row/col are ignored for {@code SHUFFLE}. */
public record Move(@NotNull MoveType type, @Min(0) int row, @Min(0) int col) {

    public static Move tap(int row, int col) {
        return new Move(MoveType.TAP, row, col);
    }
}
