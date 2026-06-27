package com.atakan.blastscale.leaderboard;

import org.springframework.boot.context.properties.ConfigurationProperties;

import java.util.Map;

/** Bound from {@code blastscale.leaderboard.*}: rank -> coins paid at season end. */
@ConfigurationProperties(prefix = "blastscale.leaderboard")
public record LeaderboardProperties(Map<Integer, Integer> rewardCoins) {
}
