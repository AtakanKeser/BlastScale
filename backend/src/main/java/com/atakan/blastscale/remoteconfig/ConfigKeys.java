package com.atakan.blastscale.remoteconfig;

import java.util.Map;

/**
 * Well-known remote configuration keys and their built-in defaults.
 *
 * <p>The defaults are only a safety net: the real values live in the {@code remote_config} table
 * (seeded by Flyway) and can be changed from the admin panel at runtime. Experiments may override
 * any of them per player.
 */
public final class ConfigKeys {

    public static final String DAILY_REWARD_COINS = "dailyRewardCoins";
    public static final String DAILY_REWARD_STREAK_BONUS = "dailyRewardStreakBonus";
    public static final String MAX_LIVES = "maxLives";
    public static final String LIFE_REGENERATION_MINUTES = "lifeRegenerationMinutes";
    public static final String LIFE_REFILL_PRICE = "lifeRefillPrice";
    public static final String BOOSTER_PRICES = "boosterPrices";
    public static final String STARTING_COINS = "startingCoins";
    public static final String LEVEL_COMPLETE_BASE_COINS = "levelCompleteBaseCoins";
    public static final String COINS_PER_STAR = "coinsPerStar";
    public static final String FIRST_CLEAR_BONUS_COINS = "firstClearBonusCoins";
    public static final String REWARD_MULTIPLIER = "rewardMultiplier";
    public static final String ROCKET_RACE_ENABLED = "rocketRaceEnabled";
    public static final String LEADERBOARD_ENABLED = "leaderboardEnabled";

    /** Fallbacks used when a key is missing from the database. */
    public static final Map<String, Object> DEFAULTS = Map.ofEntries(
            Map.entry(DAILY_REWARD_COINS, 100),
            Map.entry(DAILY_REWARD_STREAK_BONUS, 25),
            Map.entry(MAX_LIVES, 5),
            Map.entry(LIFE_REGENERATION_MINUTES, 30),
            Map.entry(LIFE_REFILL_PRICE, 150),
            Map.entry(BOOSTER_PRICES, Map.of("HAMMER", 100, "SHUFFLE", 80, "EXTRA_MOVES", 120)),
            Map.entry(STARTING_COINS, 500),
            Map.entry(LEVEL_COMPLETE_BASE_COINS, 50),
            Map.entry(COINS_PER_STAR, 25),
            Map.entry(FIRST_CLEAR_BONUS_COINS, 50),
            Map.entry(REWARD_MULTIPLIER, 1.0),
            Map.entry(ROCKET_RACE_ENABLED, true),
            Map.entry(LEADERBOARD_ENABLED, true));

    private ConfigKeys() {
    }
}
