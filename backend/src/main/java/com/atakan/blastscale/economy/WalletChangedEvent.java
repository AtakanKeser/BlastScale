package com.atakan.blastscale.economy;

/** Published after every committed wallet change; used to evict the cached player profile. */
public record WalletChangedEvent(long playerId) {
}
