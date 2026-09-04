package com.atakan.blastscale.economy.reward;

/**
 * Strategy pattern for reward calculation. Implementations are Spring beans ordered with
 * {@code @Order}; {@link RewardService} picks the first one whose {@link #supports} returns true.
 * Adding a new reward rule (a VIP bonus, a comeback reward, ...) means adding a class, not editing
 * the level-completion flow.
 */
public interface RewardStrategy {

    boolean supports(RewardContext context);

    Reward calculate(RewardContext context);
}
