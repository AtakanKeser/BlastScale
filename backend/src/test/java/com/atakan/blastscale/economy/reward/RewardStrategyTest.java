package com.atakan.blastscale.economy.reward;

import com.atakan.blastscale.experiment.PlayerExperimentAssignment;
import com.atakan.blastscale.remoteconfig.ConfigKeys;
import com.atakan.blastscale.remoteconfig.ResolvedConfig;
import org.junit.jupiter.api.Test;

import java.util.HashMap;
import java.util.List;
import java.util.Map;

import static org.assertj.core.api.Assertions.assertThat;

/** Strategy selection and arithmetic, using the built-in config defaults (base 50, 25/star, +50 first clear). */
class RewardStrategyTest {

    private final RewardService rewardService = new RewardService(List.of(
            new ExperimentRewardStrategy(), new DoubleRewardEventStrategy(), new StandardRewardStrategy()));

    private static ResolvedConfig config(Map<String, Object> overrides, List<PlayerExperimentAssignment> experiments) {
        Map<String, Object> values = new HashMap<>(ConfigKeys.DEFAULTS);
        values.putAll(overrides);
        return new ResolvedConfig(values, experiments);
    }

    @Test
    void standardRewardAddsBaseStarsAndFirstClearBonus() {
        LevelResult result = new LevelResult(3, 2000, 2, 10, 20, true);
        Reward reward = rewardService.calculate(new RewardContext(1, result, config(Map.of(), List.of()), 1.0));
        assertThat(reward.strategy()).isEqualTo(StandardRewardStrategy.NAME);
        assertThat(reward.coins()).isEqualTo(50 + 25 * 2 + 50);
        assertThat(reward.stars()).isEqualTo(2);
    }

    @Test
    void replayWithoutFirstClearHasNoBonus() {
        LevelResult result = new LevelResult(3, 2000, 3, 10, 20, false);
        Reward reward = rewardService.calculate(new RewardContext(1, result, config(Map.of(), List.of()), 1.0));
        assertThat(reward.coins()).isEqualTo(50 + 75);
    }

    @Test
    void doubleRewardEventMultipliesTheStandardReward() {
        LevelResult result = new LevelResult(3, 2000, 1, 10, 20, true);
        Reward reward = rewardService.calculate(new RewardContext(1, result, config(Map.of(), List.of()), 2.0));
        assertThat(reward.strategy()).isEqualTo(DoubleRewardEventStrategy.NAME);
        assertThat(reward.coins()).isEqualTo((50 + 25 + 50) * 2);
        assertThat(reward.multiplier()).isEqualTo(2.0);
    }

    @Test
    void experimentOverrideWinsOverEvents() {
        PlayerExperimentAssignment assignment = new PlayerExperimentAssignment(7, "reward_test", "B",
                Map.of(ConfigKeys.REWARD_MULTIPLIER, 1.5));
        // the resolved config already carries the variant's multiplier
        ResolvedConfig cfg = config(Map.of(ConfigKeys.REWARD_MULTIPLIER, 1.5), List.of(assignment));
        LevelResult result = new LevelResult(3, 2000, 0, 10, 20, false);
        Reward reward = rewardService.calculate(new RewardContext(1, result, cfg, 2.0));
        assertThat(reward.strategy()).isEqualTo(ExperimentRewardStrategy.NAME);
        assertThat(reward.coins()).isEqualTo(Math.round(50 * 1.5));
    }

    @Test
    void globalMultiplierFromRemoteConfigApplies() {
        LevelResult result = new LevelResult(3, 2000, 0, 10, 20, false);
        Reward reward = rewardService.calculate(new RewardContext(1, result, config(Map.of(ConfigKeys.REWARD_MULTIPLIER, 3.0), List.of()), 1.0));
        assertThat(reward.coins()).isEqualTo(150);
        assertThat(reward.strategy()).isEqualTo(StandardRewardStrategy.NAME);
    }
}
