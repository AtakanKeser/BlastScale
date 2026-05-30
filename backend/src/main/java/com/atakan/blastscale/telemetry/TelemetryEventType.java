package com.atakan.blastscale.telemetry;

/**
 * Every operational event the platform records. Stored as a keyword in Elasticsearch so support
 * can filter a player's history by type ("show me everything economy related for player 123").
 */
public enum TelemetryEventType {
    PLAYER_REGISTERED,
    LEVEL_STARTED,
    LEVEL_COMPLETED,
    LEVEL_FAILED,
    COMPLETION_REJECTED,
    ECONOMY_TRANSACTION,
    DAILY_REWARD_CLAIMED,
    BOOSTER_PURCHASED,
    LIVES_PURCHASED,
    LEADERBOARD_FINALIZED,
    LEADERBOARD_REWARD_GRANTED,
    EVENT_REWARD_GRANTED,
    EVENT_FINALIZED,
    EXPERIMENT_ASSIGNED,
    CONFIG_UPDATED,
    ADMIN_GRANT
}
