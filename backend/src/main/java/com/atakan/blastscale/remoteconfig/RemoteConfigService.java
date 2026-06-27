package com.atakan.blastscale.remoteconfig;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.common.redis.CacheProperties;
import com.atakan.blastscale.common.redis.RedisJsonCache;
import com.atakan.blastscale.experiment.ExperimentService;
import com.atakan.blastscale.experiment.PlayerExperimentAssignment;
import com.atakan.blastscale.telemetry.TelemetryEventType;
import com.atakan.blastscale.telemetry.TelemetryService;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import tools.jackson.databind.ObjectMapper;

import java.time.Clock;
import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Remote configuration: game tuning values that change without a client release.
 *
 * <pre>
 *   admin panel --PUT--> remote_config (MySQL) --evict--> Redis "config:base" (60s TTL)
 *                                                            |
 *   GET /api/v1/config  <-- base values + experiment overrides for the caller
 * </pre>
 */
@Service
public class RemoteConfigService {

    static final String CACHE_KEY = "config:base";

    private final RemoteConfigRepository repository;
    private final ExperimentService experimentService;
    private final RedisJsonCache cache;
    private final CacheProperties cacheProperties;
    private final ObjectMapper objectMapper;
    private final TelemetryService telemetry;
    private final Clock clock;

    public RemoteConfigService(RemoteConfigRepository repository, ExperimentService experimentService,
                               RedisJsonCache cache, CacheProperties cacheProperties, ObjectMapper objectMapper,
                               TelemetryService telemetry, Clock clock) {
        this.repository = repository;
        this.experimentService = experimentService;
        this.cache = cache;
        this.cacheProperties = cacheProperties;
        this.objectMapper = objectMapper;
        this.telemetry = telemetry;
        this.clock = clock;
    }

    /** Base configuration (defaults overlaid with database rows), cached in Redis. */
    public Map<String, Object> baseConfig() {
        return cache.getOrLoad("remote_config", CACHE_KEY, BaseConfig.class, cacheProperties.remoteConfigTtl(),
                () -> new BaseConfig(loadBaseConfig())).values();
    }

    /** Convenience for global feature flags that do not depend on the player. */
    public boolean baseConfigBoolean(String key) {
        return new ResolvedConfig(baseConfig(), List.of()).getBoolean(key);
    }

    /** Base configuration with the player's experiment variants applied on top. */
    public ResolvedConfig resolveFor(long playerId) {
        Map<String, Object> values = new LinkedHashMap<>(baseConfig());
        List<PlayerExperimentAssignment> assignments = experimentService.assignmentsFor(playerId);
        for (PlayerExperimentAssignment assignment : assignments) {
            values.putAll(assignment.overrides());
        }
        return new ResolvedConfig(values, assignments);
    }

    @Transactional(readOnly = true)
    public List<RemoteConfigEntry> listEntries() {
        return repository.findAllByOrderByKeyAsc();
    }

    /** Creates or updates a key. The new value is visible to players once the cache expires (<= 60s). */
    @Transactional
    public RemoteConfigEntry update(String key, Object value, String description, String updatedBy) {
        if (value == null) {
            throw new BlastScaleException(ErrorCode.VALIDATION_ERROR, "A value is required");
        }
        String json = objectMapper.writeValueAsString(value);
        Instant now = Instant.now(clock);
        RemoteConfigEntry entry = repository.findById(key)
                .map(existing -> {
                    existing.update(json, description, now, updatedBy);
                    return existing;
                })
                .orElseGet(() -> repository.save(new RemoteConfigEntry(key, json, description, now, updatedBy)));
        cache.evict(CACHE_KEY);
        telemetry.record(TelemetryEventType.CONFIG_UPDATED, null, "config", key,
                Map.of("value", value, "updatedBy", updatedBy));
        return entry;
    }

    @Transactional(readOnly = true)
    protected Map<String, Object> loadBaseConfig() {
        Map<String, Object> values = new LinkedHashMap<>(ConfigKeys.DEFAULTS);
        for (RemoteConfigEntry entry : repository.findAll()) {
            values.put(entry.getKey(), objectMapper.readValue(entry.getValueJson(), Object.class));
        }
        return values;
    }

    /** Cache envelope (a concrete class is needed to deserialize the map back from Redis). */
    record BaseConfig(Map<String, Object> values) {
    }
}
