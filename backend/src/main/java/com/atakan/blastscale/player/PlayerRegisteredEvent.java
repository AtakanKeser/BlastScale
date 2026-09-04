package com.atakan.blastscale.player;

/**
 * Published (synchronously, inside the registration transaction) when a new player row is
 * created. Other modules react to it without the player module knowing about them — the economy
 * module, for example, creates the wallet with the starting coins and lives.
 */
public record PlayerRegisteredEvent(long playerId, String username, boolean guest) {
}
