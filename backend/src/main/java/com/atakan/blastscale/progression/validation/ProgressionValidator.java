package com.atakan.blastscale.progression.validation;

import com.atakan.blastscale.common.exception.ErrorCode;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;

/** The player may only complete levels they have unlocked. */
@Component
@Order(200)
public class ProgressionValidator implements CompletionValidator {

    @Override
    public String name() {
        return "progression";
    }

    @Override
    public ValidationResult validate(LevelCompletionContext ctx) {
        int level = ctx.level().getLevelNumber();
        if (level > ctx.player().getCurrentLevel()) {
            return ValidationResult.fail(ErrorCode.LEVEL_LOCKED,
                    "Level " + level + " is locked; current level is " + ctx.player().getCurrentLevel());
        }
        return ValidationResult.ok();
    }
}
