package com.atakan.blastscale.economy.reward;

/**
 * What the player receives for a level.
 *
 * @param strategy name of the strategy that produced it — surfaced to the client and telemetry so
 *                 "why did I get 200 coins?" is always answerable
 */
public record Reward(long coins, int stars, double multiplier, String strategy) {
}
