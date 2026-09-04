package com.atakan.blastscale.experiment;

import java.util.Map;

/**
 * One arm of an experiment.
 *
 * @param name      e.g. {@code "A"} / {@code "control"}
 * @param weight    traffic share in percent; all weights of an experiment sum to 100
 * @param overrides remote-config keys this variant overrides for its players,
 *                  e.g. {@code {"lifeRegenerationMinutes": 25}}
 */
public record ExperimentVariant(String name, int weight, Map<String, Object> overrides) {

    public ExperimentVariant {
        overrides = overrides == null ? Map.of() : Map.copyOf(overrides);
    }
}
