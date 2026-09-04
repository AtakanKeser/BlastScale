package com.atakan.blastscale.economy.reward;

import com.atakan.blastscale.remoteconfig.ConfigKeys;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;

/**
 * Default rule, always applicable:
 * <pre>coins = (base + coinsPerStar * stars + firstClearBonus) * rewardMultiplier</pre>
 * Every number comes from remote config.
 */
@Component
@Order(300)
public class StandardRewardStrategy implements RewardStrategy {

    public static final String NAME = "STANDARD";

    @Override
    public boolean supports(RewardContext context) {
        return true;
    }

    @Override
    public Reward calculate(RewardContext context) {
        return calculate(context, context.config().getDouble(ConfigKeys.REWARD_MULTIPLIER), NAME);
    }

    /** Shared arithmetic reused by the other strategies with a different multiplier/name. */
    static Reward calculate(RewardContext context, double multiplier, String strategyName) {
        var cfg = context.config();
        var result = context.result();
        long base = cfg.getInt(ConfigKeys.LEVEL_COMPLETE_BASE_COINS)
                + (long) cfg.getInt(ConfigKeys.COINS_PER_STAR) * result.stars()
                + (result.firstClear() ? cfg.getInt(ConfigKeys.FIRST_CLEAR_BONUS_COINS) : 0);
        long coins = Math.round(base * multiplier);
        return new Reward(coins, result.stars(), multiplier, strategyName);
    }
}
