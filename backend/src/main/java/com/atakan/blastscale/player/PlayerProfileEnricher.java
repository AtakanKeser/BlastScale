package com.atakan.blastscale.player;

/**
 * Extension point through which another module (the economy module) contributes wallet data to
 * the player profile without the player module depending on it.
 */
public interface PlayerProfileEnricher {

    PlayerProfile.WalletSummary walletSummary(long playerId);
}
