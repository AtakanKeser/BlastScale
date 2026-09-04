package com.atakan.blastscale.progression.validation;

import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.level.engine.BoardConfig;
import com.atakan.blastscale.level.engine.MoveType;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;

import java.util.Map;

/**
 * Cheap plausibility bounds before the replay: move count within the limit, claimed score below
 * the theoretical maximum, boosters actually owned.
 */
@Component
@Order(400)
public class ScoreValidator implements CompletionValidator {

    @Override
    public String name() {
        return "score";
    }

    @Override
    public ValidationResult validate(LevelCompletionContext ctx) {
        BoardConfig board = ctx.level().toBoardConfig();
        int moveLimit = board.moveLimit() + (ctx.extraMovesUsed() ? BoardConfig.EXTRA_MOVES_BONUS : 0);
        long taps = ctx.countMoves(MoveType.TAP);
        if (taps > moveLimit || ctx.claimedMoves() > moveLimit) {
            return ValidationResult.fail(ErrorCode.TOO_MANY_MOVES, "More moves than the level allows",
                    Map.of("moves", taps, "moveLimit", moveLimit));
        }
        // Even popping the whole board on every move cannot exceed this.
        long cells = (long) board.rows() * board.cols();
        long maxScore = taps * BoardConfig.groupScore((int) cells);
        if (ctx.claimedScore() > maxScore) {
            return ValidationResult.fail(ErrorCode.SCORE_OUT_OF_RANGE, "Claimed score is impossible for this level",
                    Map.of("claimedScore", ctx.claimedScore(), "maxScore", maxScore));
        }
        long hammers = ctx.countMoves(MoveType.HAMMER);
        long shuffles = ctx.countMoves(MoveType.SHUFFLE);
        Map<String, Integer> owned = ctx.wallet().boosters();
        if (hammers > owned.getOrDefault("HAMMER", 0)
                || shuffles > owned.getOrDefault("SHUFFLE", 0)
                || (ctx.extraMovesUsed() && owned.getOrDefault("EXTRA_MOVES", 0) < 1)) {
            return ValidationResult.fail(ErrorCode.INSUFFICIENT_BOOSTERS, "Used boosters the player does not own",
                    Map.of("hammers", hammers, "shuffles", shuffles, "extraMoves", ctx.extraMovesUsed(), "owned", owned));
        }
        return ValidationResult.ok();
    }
}
