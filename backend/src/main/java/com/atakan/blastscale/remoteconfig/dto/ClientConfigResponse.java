package com.atakan.blastscale.remoteconfig.dto;

import java.time.Instant;
import java.util.List;
import java.util.Map;

/**
 * Payload of {@code GET /api/v1/config}: everything the client needs to tune itself on launch.
 *
 * @param config      effective key/value configuration for this player
 * @param experiments experiments the player is assigned to (id, key, variant)
 * @param serverTime  lets the client compute countdowns without trusting the device clock
 */
public record ClientConfigResponse(
        Map<String, Object> config,
        List<ExperimentAssignmentView> experiments,
        Instant serverTime) {

    public record ExperimentAssignmentView(long id, String key, String variant) {
    }
}
