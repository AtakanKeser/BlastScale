package com.atakan.blastscale.economy.reward;

import com.atakan.blastscale.remoteconfig.ConfigKeys;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;

/**
 * Highest priority: when the player is in an experiment that overrides {@code rewardMultiplier},
 * that value is used <b>exclusively</b> (event multipliers are ignored) so the experiment measures
 * exactly one change. The already-resolved config carries the variant's multiplier.
 */
@Component
@Order(100)
public class ExperimentRewardStrategy implements RewardStrategy {

    public static final String NAME = "EXPERIMENT";

    @Override
    public boolean supports(RewardContext context) {
        return context.hasExperimentRewardOverride();
    }

    @Override
    public Reward calculate(RewardContext context) {
        return StandardRewardStrategy.calculate(context, context.config().getDouble(ConfigKeys.REWARD_MULTIPLIER), NAME);
    }
}
