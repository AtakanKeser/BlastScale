package com.atakan.blastscale.event;

import java.util.Map;

/**
 * Typed view of an event's JSON configuration. Because the rules live in data, a new Rocket Race
 * with different points or prizes is a row in the database, not a deploy.
 */
public sealed interface EventRule permits EventRule.RocketRaceRule, EventRule.DoubleRewardRule {

    /**
     * @param pointsPerLevel rockets earned per completed level
     * @param minimumLevel   players below this level do not participate (protects the tutorial)
     * @param rewards        rank -> coins
     */
    record RocketRaceRule(int pointsPerLevel, int minimumLevel, Map<Integer, Integer> rewards) implements EventRule {
    }

    /** @param multiplier applied to level rewards, e.g. 2.0 for a "Double Reward Weekend" */
    record DoubleRewardRule(double multiplier) implements EventRule {
    }
}
