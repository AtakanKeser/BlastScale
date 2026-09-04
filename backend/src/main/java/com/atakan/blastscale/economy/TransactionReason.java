package com.atakan.blastscale.economy;

/**
 * Why a ledger entry exists. Together with {@code reference_id} it forms the uniqueness rule
 * "one entry per (player, reason, reference, resource)" that makes rewards exactly-once at the
 * database level.
 */
public enum TransactionReason {
    INITIAL_GRANT,
    LEVEL_START,
    LEVEL_COMPLETE,
    BUY_BOOSTER,
    USE_BOOSTER,
    BUY_LIVES,
    DAILY_REWARD,
    LEADERBOARD_REWARD,
    EVENT_REWARD,
    ADMIN_GRANT
}
