package com.atakan.blastscale.economy.reward;

import org.springframework.stereotype.Service;

import java.util.List;

/** Picks the applicable {@link RewardStrategy} (beans are injected in {@code @Order} order). */
@Service
public class RewardService {

    private final List<RewardStrategy> strategies;

    public RewardService(List<RewardStrategy> strategies) {
        this.strategies = strategies;
    }

    public Reward calculate(RewardContext context) {
        for (RewardStrategy strategy : strategies) {
            if (strategy.supports(context)) {
                return strategy.calculate(context);
            }
        }
        throw new IllegalStateException("No reward strategy applies; StandardRewardStrategy should always match");
    }
}
