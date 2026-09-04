package com.atakan.blastscale.remoteconfig;

import com.atakan.blastscale.experiment.PlayerExperimentAssignment;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Effective configuration for one player: base values from the database with the player's
 * experiment overrides applied on top. Typed accessors fall back to {@link ConfigKeys#DEFAULTS}
 * so a missing or malformed value never breaks gameplay.
 */
public record ResolvedConfig(Map<String, Object> values, List<PlayerExperimentAssignment> experiments) {

    public int getInt(String key) {
        Object value = valueOrDefault(key);
        return value instanceof Number n ? n.intValue() : Integer.parseInt(value.toString());
    }

    public double getDouble(String key) {
        Object value = valueOrDefault(key);
        return value instanceof Number n ? n.doubleValue() : Double.parseDouble(value.toString());
    }

    public boolean getBoolean(String key) {
        Object value = valueOrDefault(key);
        return value instanceof Boolean b ? b : Boolean.parseBoolean(value.toString());
    }

    @SuppressWarnings("unchecked")
    public Map<String, Integer> getIntMap(String key) {
        Object value = valueOrDefault(key);
        Map<String, Integer> result = new LinkedHashMap<>();
        if (value instanceof Map<?, ?> map) {
            ((Map<String, Object>) map).forEach((k, v) -> result.put(k, v instanceof Number n ? n.intValue() : Integer.parseInt(v.toString())));
        }
        return result;
    }

    public int maxLives() {
        return getInt(ConfigKeys.MAX_LIVES);
    }

    public int lifeRegenerationMinutes() {
        return getInt(ConfigKeys.LIFE_REGENERATION_MINUTES);
    }

    private Object valueOrDefault(String key) {
        Object value = values.get(key);
        if (value == null) {
            value = ConfigKeys.DEFAULTS.get(key);
        }
        if (value == null) {
            throw new IllegalArgumentException("Unknown config key " + key);
        }
        return value;
    }
}
