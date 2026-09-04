package com.atakan.blastscale.economy.reward;

import com.atakan.blastscale.remoteconfig.ConfigKeys;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;

/**
 * Applies while a DOUBLE_REWARD live event is active: the standard reward times the event's
 * multiplier (configured per event, typically 2.0). Started from the admin panel, no deploy.
 */
@Component
@Order(200)
public class DoubleRewardEventStrategy implements RewardStrategy {

    public static final String NAME = "DOUBLE_REWARD_EVENT";

    @Override
    public boolean supports(RewardContext context) {
        return context.doubleRewardMultiplier() > 1.0;
    }

    @Override
    public Reward calculate(RewardContext context) {
        double multiplier = context.config().getDouble(ConfigKeys.REWARD_MULTIPLIER) * context.doubleRewardMultiplier();
        return StandardRewardStrategy.calculate(context, multiplier, NAME);
    }
}
