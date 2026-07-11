package com.atakan.blastscale.progression.validation;

import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.level.engine.BoardEngine;
import com.atakan.blastscale.level.engine.SimulationResult;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;

import java.util.Map;

/**
 * The decisive check: replay the moves on the server's own copy of the board (same seed, same
 * engine) and compare. The client's score is a claim; the replay's score is the truth.
 */
@Component
@Order(500)
public class ReplayValidator implements CompletionValidator {

    @Override
    public String name() {
        return "replay";
    }

    @Override
    public ValidationResult validate(LevelCompletionContext ctx) {
        SimulationResult result = BoardEngine.simulate(ctx.level().toBoardConfig(), ctx.session().getSeed(),
                ctx.moves(), ctx.extraMovesUsed());
        ctx.setSimulation(result);
        if (!result.valid()) {
            return ValidationResult.fail(ErrorCode.INVALID_MOVE_SEQUENCE, "Move sequence is not legal: " + result.rejectionReason());
        }
        if (result.score() != ctx.claimedScore()) {
            return ValidationResult.fail(ErrorCode.SCORE_MISMATCH, "Reported score does not match the replay",
                    Map.of("claimedScore", ctx.claimedScore(), "serverScore", result.score()));
        }
        if (!result.objectiveReached()) {
            return ValidationResult.fail(ErrorCode.OBJECTIVE_NOT_REACHED, "Target score was not reached",
                    Map.of("score", result.score(), "targetScore", ctx.level().getTargetScore()));
        }
        return ValidationResult.ok();
    }
}
