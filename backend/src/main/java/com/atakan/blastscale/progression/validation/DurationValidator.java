package com.atakan.blastscale.progression.validation;

import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.level.engine.MoveType;
import com.atakan.blastscale.progression.GameplayProperties;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;

import java.time.Duration;
import java.util.Map;

/**
 * A human cannot play 20 moves in 200 milliseconds. Bots that start and instantly complete a level
 * are rejected here before the (more expensive) replay runs.
 */
@Component
@Order(300)
public class DurationValidator implements CompletionValidator {

    private final GameplayProperties properties;

    public DurationValidator(GameplayProperties properties) {
        this.properties = properties;
    }

    @Override
    public String name() {
        return "duration";
    }

    @Override
    public ValidationResult validate(LevelCompletionContext ctx) {
        long elapsedMillis = Duration.between(ctx.session().getStartedAt(), ctx.now()).toMillis();
        long taps = ctx.countMoves(MoveType.TAP);
        long minimum = taps * properties.minMillisPerMove();
        if (elapsedMillis < minimum) {
            return ValidationResult.fail(ErrorCode.SUSPICIOUS_DURATION,
                    "Level completed too quickly to be played by a human",
                    Map.of("elapsedMillis", elapsedMillis, "minimumMillis", minimum, "moves", taps));
        }
        return ValidationResult.ok();
    }
}
