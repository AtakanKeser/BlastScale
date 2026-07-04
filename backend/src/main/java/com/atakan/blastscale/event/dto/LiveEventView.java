package com.atakan.blastscale.event.dto;

import java.time.Instant;
import java.util.List;
import java.util.Map;

/**
 * Admin/API view of an event (also the shape cached in Redis for the active-event list).
 *
 * @param configuration the raw rule JSON as a map
 * @param participants  number of participating players (admin views only, else {@code null})
 * @param top           current top ranks (admin views only)
 */
public record LiveEventView(
        long id,
        String type,
        String name,
        String status,
        Instant startAt,
        Instant endAt,
        Map<String, Object> configuration,
        Long participants,
        List<Standing> top,
        Instant createdAt,
        Instant updatedAt) {

    public record Standing(int rank, long playerId, String name, long points, Integer rewardCoins) {
    }
}
