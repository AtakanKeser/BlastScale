package com.atakan.blastscale.event.dto;

import java.time.Instant;
import java.util.List;
import java.util.Map;

/**
 * What the game client shows on the event screen: the event, the player's own standing and the
 * current leaders.
 */
public record PlayerEventView(
        long id,
        String type,
        String name,
        Instant startAt,
        Instant endAt,
        long secondsRemaining,
        Map<String, Object> configuration,
        long myPoints,
        Integer myRank,
        boolean eligible,
        List<LiveEventView.Standing> top) {
}
