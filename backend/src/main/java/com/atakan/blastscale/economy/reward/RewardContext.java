package com.atakan.blastscale.economy.reward;

import com.atakan.blastscale.remoteconfig.ConfigKeys;
import com.atakan.blastscale.remoteconfig.ResolvedConfig;

/**
 * Everything a {@link RewardStrategy} may look at. Live-event state is passed in by the caller so
 * the economy module does not depend on the event module.
 *
 * @param doubleRewardMultiplier multiplier of an active double-reward event, or {@code 1.0}
 */
public record RewardContext(long playerId, LevelResult result, ResolvedConfig config, double doubleRewardMultiplier) {

    /** True when one of the player's experiments overrides the reward multiplier. */
    public boolean hasExperimentRewardOverride() {
        return config.experiments().stream()
                .anyMatch(a -> a.overrides().containsKey(ConfigKeys.REWARD_MULTIPLIER));
    }
}
