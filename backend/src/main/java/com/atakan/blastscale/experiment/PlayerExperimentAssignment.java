package com.atakan.blastscale.experiment;

import java.util.Map;

/** What the client and the config resolver need to know about one assignment. */
public record PlayerExperimentAssignment(long experimentId, String key, String variant, Map<String, Object> overrides) {
}
