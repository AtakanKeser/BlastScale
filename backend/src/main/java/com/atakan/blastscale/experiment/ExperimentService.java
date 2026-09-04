package com.atakan.blastscale.experiment;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.common.redis.RedisJsonCache;
import com.atakan.blastscale.experiment.dto.CreateExperimentRequest;
import com.atakan.blastscale.experiment.dto.ExperimentView;
import com.atakan.blastscale.telemetry.TelemetryEventType;
import com.atakan.blastscale.telemetry.TelemetryService;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import tools.jackson.core.type.TypeReference;
import tools.jackson.databind.ObjectMapper;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import java.util.function.Function;
import java.util.stream.Collectors;

/**
 * A/B experimentation.
 *
 * <p>Read path ({@link #assignmentsFor}) is hit on every {@code GET /api/v1/config} and on every
 * life-regeneration calculation, so it is cached in Redis per player for a minute; the list of
 * live experiments is cached globally for the same time. Changes made in the admin panel therefore
 * reach all players within a minute without any deploy.
 */
@Service
public class ExperimentService {

    private static final Duration CACHE_TTL = Duration.ofSeconds(60);
    private static final String LIVE_KEY = "experiments:live";
    private static final TypeReference<List<ExperimentVariant>> VARIANTS = new TypeReference<>() {
    };

    private final ExperimentRepository experiments;
    private final ExperimentAssignmentRepository assignments;
    private final RedisJsonCache cache;
    private final ObjectMapper objectMapper;
    private final TelemetryService telemetry;
    private final Clock clock;

    public ExperimentService(ExperimentRepository experiments, ExperimentAssignmentRepository assignments,
                             RedisJsonCache cache, ObjectMapper objectMapper, TelemetryService telemetry, Clock clock) {
        this.experiments = experiments;
        this.assignments = assignments;
        this.cache = cache;
        this.objectMapper = objectMapper;
        this.telemetry = telemetry;
        this.clock = clock;
    }

    // ------------------------------------------------------------------ player side

    /** Every live experiment the player is (now) assigned to. */
    public List<PlayerExperimentAssignment> assignmentsFor(long playerId) {
        List<ExperimentView> live = liveExperiments();
        if (live.isEmpty()) {
            return List.of();
        }
        // The fingerprint of the live experiment set is part of the key: starting, pausing or
        // ending an experiment in the admin panel changes the fingerprint, so players fall through
        // to a fresh resolution immediately instead of waiting for the per-player TTL.
        String key = "experiments:player:" + playerId + ":" + fingerprint(live);
        return cache.getOrLoad("experiment_assignments", key, PlayerAssignments.class, CACHE_TTL,
                () -> new PlayerAssignments(resolveAssignments(playerId, live))).assignments();
    }

    /** Config overrides of all assigned variants, merged in experiment id order. */
    public Map<String, Object> overridesFor(long playerId) {
        Map<String, Object> merged = new LinkedHashMap<>();
        for (PlayerExperimentAssignment assignment : assignmentsFor(playerId)) {
            merged.putAll(assignment.overrides());
        }
        return merged;
    }

    @Transactional
    protected List<PlayerExperimentAssignment> resolveAssignments(long playerId, List<ExperimentView> live) {
        Map<Long, ExperimentAssignment> stored = assignments.findByIdPlayerId(playerId).stream()
                .collect(Collectors.toMap(a -> a.getId().getExperimentId(), Function.identity()));

        List<PlayerExperimentAssignment> result = new ArrayList<>();
        Instant now = Instant.now(clock);
        for (ExperimentView experiment : live) {
            String variantName;
            ExperimentAssignment existing = stored.get(experiment.id());
            if (existing != null) {
                variantName = existing.getVariant();
            } else {
                int bucket = Bucketing.bucket(playerId, experiment.key());
                variantName = Bucketing.pick(experiment.variants(), bucket).name();
                int inserted = assignments.insertIfAbsent(experiment.id(), playerId, variantName, bucket, now);
                if (inserted == 1) {
                    telemetry.record(TelemetryEventType.EXPERIMENT_ASSIGNED, playerId, "experiment",
                            experiment.key(), Map.of("variant", variantName, "bucket", bucket));
                } else {
                    // Lost the race against another device of the same player: use the stored row.
                    variantName = assignments.findById(new ExperimentAssignmentId(experiment.id(), playerId))
                            .map(ExperimentAssignment::getVariant).orElse(variantName);
                }
            }
            Map<String, Object> overrides = variantByName(experiment.variants(), variantName)
                    .map(ExperimentVariant::overrides).orElse(Map.of());
            result.add(new PlayerExperimentAssignment(experiment.id(), experiment.key(), variantName, overrides));
        }
        return result;
    }

    /** Live experiments, cached globally for a minute. */
    public List<ExperimentView> liveExperiments() {
        Instant now = Instant.now(clock);
        return cache.getOrLoad("experiments_live", LIVE_KEY, LiveExperiments.class, CACHE_TTL,
                () -> new LiveExperiments(experiments.findByStatus(ExperimentStatus.RUNNING).stream()
                        .filter(e -> e.isLive(now))
                        .map(e -> toView(e, false))
                        .toList())).experiments();
    }

    // ------------------------------------------------------------------ admin side

    @Transactional(readOnly = true)
    public List<ExperimentView> listAll() {
        return experiments.findAllByOrderByIdDesc().stream().map(e -> toView(e, true)).toList();
    }

    @Transactional(readOnly = true)
    public ExperimentView get(long id) {
        return toView(require(id), true);
    }

    @Transactional
    public ExperimentView create(CreateExperimentRequest request) {
        validateVariants(request.variants());
        if (experiments.findByKey(request.key()).isPresent()) {
            throw new BlastScaleException(ErrorCode.CONFLICT, "Experiment key '" + request.key() + "' already exists");
        }
        Experiment experiment = new Experiment(request.key(), request.name(),
                objectMapper.writeValueAsString(request.variants()), request.startAt(), request.endAt(), Instant.now(clock));
        return toView(experiments.save(experiment), false);
    }

    @Transactional
    public ExperimentView transition(long id, ExperimentStatus target) {
        Experiment experiment = require(id);
        ExperimentStatus current = experiment.getStatus();
        boolean allowed = switch (target) {
            case RUNNING -> current == ExperimentStatus.DRAFT || current == ExperimentStatus.PAUSED;
            case PAUSED -> current == ExperimentStatus.RUNNING;
            case ENDED -> current != ExperimentStatus.ENDED;
            case DRAFT -> false;
        };
        if (!allowed) {
            throw new BlastScaleException(ErrorCode.EXPERIMENT_INVALID_STATE,
                    "Cannot move experiment from " + current + " to " + target);
        }
        experiment.setStatus(target, Instant.now(clock));
        cache.evict(LIVE_KEY); // players pick the change up as their per-player cache expires
        return toView(experiment, true);
    }

    // ------------------------------------------------------------------ helpers

    static void validateVariants(List<ExperimentVariant> variants) {
        int total = 0;
        Set<String> names = new HashSet<>();
        for (ExperimentVariant variant : variants) {
            if (variant.name() == null || variant.name().isBlank() || variant.weight() < 0) {
                throw new BlastScaleException(ErrorCode.EXPERIMENT_INVALID_VARIANTS, "Variants need a name and a non-negative weight");
            }
            if (!names.add(variant.name())) {
                throw new BlastScaleException(ErrorCode.EXPERIMENT_INVALID_VARIANTS, "Duplicate variant name " + variant.name());
            }
            total += variant.weight();
        }
        if (total != Bucketing.BUCKETS) {
            throw new BlastScaleException(ErrorCode.EXPERIMENT_INVALID_VARIANTS, "Variant weights must sum to 100, got " + total);
        }
    }

    private Experiment require(long id) {
        return experiments.findById(id)
                .orElseThrow(() -> new BlastScaleException(ErrorCode.EXPERIMENT_NOT_FOUND, "Experiment " + id + " does not exist"));
    }

    private ExperimentView toView(Experiment experiment, boolean withCounts) {
        List<ExperimentVariant> variants = objectMapper.readValue(experiment.getVariantsJson(), VARIANTS);
        Map<String, Long> counts = new LinkedHashMap<>();
        if (withCounts) {
            variants.forEach(v -> counts.put(v.name(), 0L));
            assignments.countByVariant(experiment.getId()).forEach(c -> counts.put(c.getVariant(), c.getCount()));
        }
        return new ExperimentView(experiment.getId(), experiment.getKey(), experiment.getName(),
                experiment.getStatus().name(), experiment.getStartAt(), experiment.getEndAt(), variants,
                withCounts ? counts : null, experiment.getCreatedAt(), experiment.getUpdatedAt());
    }

    private static String fingerprint(List<ExperimentView> live) {
        return live.stream()
                .map(e -> e.id() + "@" + (e.updatedAt() == null ? 0 : e.updatedAt().toEpochMilli()))
                .collect(Collectors.joining(","));
    }

    private static Optional<ExperimentVariant> variantByName(List<ExperimentVariant> variants, String name) {
        return variants.stream().filter(v -> v.name().equals(name)).findFirst();
    }

    /** Cache envelopes: RedisJsonCache needs a concrete class to deserialize generic lists. */
    record PlayerAssignments(List<PlayerExperimentAssignment> assignments) {
    }

    record LiveExperiments(List<ExperimentView> experiments) {
    }
}
