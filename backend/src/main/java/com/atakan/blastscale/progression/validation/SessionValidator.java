package com.atakan.blastscale.progression.validation;

import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.progression.GameplayProperties;
import com.atakan.blastscale.progression.SessionStatus;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;

import java.time.Duration;

/** Is this a live session of this player for this level? */
@Component
@Order(100)
public class SessionValidator implements CompletionValidator {

    private final GameplayProperties properties;

    public SessionValidator(GameplayProperties properties) {
        this.properties = properties;
    }

    @Override
    public String name() {
        return "session";
    }

    @Override
    public ValidationResult validate(LevelCompletionContext ctx) {
        var session = ctx.session();
        if (!session.getPlayerId().equals(ctx.player().getId())) {
            // Never reveal that the session exists for somebody else.
            return ValidationResult.fail(ErrorCode.SESSION_NOT_FOUND, "Session not found");
        }
        if (session.getStatus() != SessionStatus.ACTIVE) {
            return ValidationResult.fail(ErrorCode.SESSION_NOT_ACTIVE, "Session is " + session.getStatus());
        }
        if (session.getLevelId() != ctx.level().getLevelNumber()) {
            return ValidationResult.fail(ErrorCode.SESSION_LEVEL_MISMATCH,
                    "Session belongs to level " + session.getLevelId() + ", not " + ctx.level().getLevelNumber());
        }
        if (Duration.between(session.getStartedAt(), ctx.now()).compareTo(properties.sessionTtl()) > 0) {
            return ValidationResult.fail(ErrorCode.SESSION_EXPIRED, "Session expired, start the level again");
        }
        return ValidationResult.ok();
    }
}
