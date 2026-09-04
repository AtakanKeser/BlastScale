package com.atakan.blastscale.level;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.common.redis.CacheProperties;
import com.atakan.blastscale.common.redis.RedisJsonCache;
import com.atakan.blastscale.level.dto.UpsertLevelRequest;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataAccessException;
import org.springframework.stereotype.Service;

import java.time.Clock;
import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.Optional;

/**
 * Level lookups with two fallbacks:
 * <pre>
 *   Redis "level:{n}" --miss--> MongoDB "levels" --miss/down--> procedural generator
 * </pre>
 * A generated level is written back to MongoDB (best effort) so designers can tweak it later.
 * A MongoDB outage therefore degrades to procedural levels instead of stopping gameplay.
 */
@Service
public class LevelDefinitionService {

    private static final Logger log = LoggerFactory.getLogger(LevelDefinitionService.class);
    private static final int MAX_LEVEL = 10_000;

    private final LevelDefinitionRepository repository;
    private final RedisJsonCache cache;
    private final CacheProperties cacheProperties;
    private final Clock clock;

    public LevelDefinitionService(LevelDefinitionRepository repository, RedisJsonCache cache,
                                  CacheProperties cacheProperties, Clock clock) {
        this.repository = repository;
        this.cache = cache;
        this.cacheProperties = cacheProperties;
        this.clock = clock;
    }

    public LevelDefinition get(int levelNumber) {
        if (levelNumber < 1 || levelNumber > MAX_LEVEL) {
            throw new BlastScaleException(ErrorCode.LEVEL_NOT_FOUND, "Level " + levelNumber + " does not exist");
        }
        return cache.getOrLoad("level_definition", cacheKey(levelNumber), LevelDefinition.class,
                cacheProperties.levelDefinitionTtl(), () -> load(levelNumber));
    }

    private LevelDefinition load(int levelNumber) {
        try {
            Optional<LevelDefinition> stored = repository.findById(LevelDefinition.idFor(levelNumber));
            if (stored.isPresent()) {
                return stored.get();
            }
            LevelDefinition generated = ProceduralLevelGenerator.generate(levelNumber, Instant.now(clock));
            repository.save(generated);
            return generated;
        } catch (DataAccessException e) {
            log.warn("MongoDB unavailable for level {}, using procedural definition: {}", levelNumber, e.getMessage());
            return ProceduralLevelGenerator.generate(levelNumber, Instant.now(clock));
        }
    }

    /** Admin: hand-tuned level replaces the generated one; the cache is evicted immediately. */
    public LevelDefinition upsert(int levelNumber, UpsertLevelRequest request) {
        if (request.starThresholds().size() != 3 || request.starThresholds().get(0) != request.targetScore()) {
            throw new BlastScaleException(ErrorCode.VALIDATION_ERROR,
                    "starThresholds must have 3 ascending values and start with targetScore");
        }
        int version = repository.findById(LevelDefinition.idFor(levelNumber)).map(l -> l.getVersion() + 1).orElse(1);
        LevelDefinition definition = new LevelDefinition(levelNumber, version, request.rows(), request.cols(),
                request.colorCount(), request.moveLimit(), request.targetScore(), request.starThresholds(),
                request.specialRules() == null ? Map.of() : request.specialRules(), "admin", Instant.now(clock));
        repository.save(definition);
        cache.evict(cacheKey(levelNumber));
        return definition;
    }

    public List<LevelDefinition> list(int from, int to) {
        return repository.findByLevelNumberBetweenOrderByLevelNumberAsc(from, to);
    }

    private static String cacheKey(int levelNumber) {
        return "level:" + levelNumber;
    }
}
