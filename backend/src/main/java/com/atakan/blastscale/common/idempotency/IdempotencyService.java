package com.atakan.blastscale.common.idempotency;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.common.metrics.GameplayMetrics;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataAccessException;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Service;
import tools.jackson.databind.ObjectMapper;

import java.time.Duration;
import java.util.function.Supplier;

/**
 * Makes state-changing requests safe to retry.
 *
 * <p>Mobile networks drop responses all the time: the client sends {@code POST /complete}, the
 * server credits 100 coins, the response never arrives, the client retries. Without protection
 * the player is paid twice. The client therefore sends an {@code Idempotency-Key} header (a UUID
 * generated once per logical action) and this service guarantees that the work behind a key is
 * executed <b>at most once</b>:
 *
 * <pre>
 *   SET idem:{scope}:{key} "IN_PROGRESS" NX EX ttl
 *      |                       |
 *   acquired               already exists
 *      |                       |
 *   run work           value == IN_PROGRESS ? -> 409 IDEMPOTENT_REQUEST_IN_PROGRESS
 *      |                       |
 *   store JSON result   return stored JSON result (replayed = true)
 * </pre>
 *
 * <p>Redis is the fast path. The database provides the second, stronger line of defence with
 * unique constraints (e.g. one LEVEL_COMPLETE ledger entry per session), so even if Redis is
 * unavailable — in which case we deliberately proceed without the guard rather than fail the
 * request — a duplicate can never be paid.
 */
@Service
public class IdempotencyService {

    private static final Logger log = LoggerFactory.getLogger(IdempotencyService.class);
    private static final String IN_PROGRESS = "__IN_PROGRESS__";
    private static final Duration IN_PROGRESS_TTL = Duration.ofSeconds(30);

    private final StringRedisTemplate redis;
    private final ObjectMapper objectMapper;
    private final IdempotencyProperties properties;
    private final GameplayMetrics metrics;

    public IdempotencyService(StringRedisTemplate redis, ObjectMapper objectMapper,
                              IdempotencyProperties properties, GameplayMetrics metrics) {
        this.redis = redis;
        this.objectMapper = objectMapper;
        this.properties = properties;
        this.metrics = metrics;
    }

    /**
     * Executes {@code work} at most once for the given scope + key.
     *
     * @param scope logical operation, e.g. {@code level-complete}; keys are namespaced per scope
     *              and per player by the caller so two players can never collide
     * @param key   the client supplied Idempotency-Key (may be null: then no guard is applied)
     */
    public <T> IdempotentResult<T> execute(String scope, String key, Class<T> type, Supplier<T> work) {
        if (key == null || key.isBlank()) {
            return new IdempotentResult<>(work.get(), false);
        }
        String redisKey = "idem:" + scope + ":" + key;

        Boolean acquired;
        try {
            acquired = redis.opsForValue().setIfAbsent(redisKey, IN_PROGRESS, IN_PROGRESS_TTL);
        } catch (DataAccessException e) {
            log.warn("Redis unavailable for idempotency key {}; relying on database constraints", redisKey);
            return new IdempotentResult<>(work.get(), false);
        }

        if (!Boolean.TRUE.equals(acquired)) {
            return replay(scope, redisKey, type);
        }

        T result;
        try {
            result = work.get();
        } catch (RuntimeException e) {
            // The work failed: forget the key so the client can retry the *same* request.
            safeDelete(redisKey);
            throw e;
        }
        try {
            redis.opsForValue().set(redisKey, objectMapper.writeValueAsString(result), properties.ttl());
        } catch (DataAccessException e) {
            log.warn("Could not store idempotent response for {}: {}", redisKey, e.getMessage());
        }
        return new IdempotentResult<>(result, false);
    }

    private <T> IdempotentResult<T> replay(String scope, String redisKey, Class<T> type) {
        String stored = redis.opsForValue().get(redisKey);
        if (stored == null) {
            // The IN_PROGRESS marker expired or the original request failed and cleaned up.
            // Treat it as a conflict: the client should retry with the same key shortly.
            throw new BlastScaleException(ErrorCode.IDEMPOTENT_REQUEST_IN_PROGRESS,
                    "A request with this Idempotency-Key is being processed, retry shortly");
        }
        if (IN_PROGRESS.equals(stored)) {
            throw new BlastScaleException(ErrorCode.IDEMPOTENT_REQUEST_IN_PROGRESS,
                    "A request with this Idempotency-Key is still being processed");
        }
        metrics.idempotentReplay(scope);
        log.info("Replaying stored response for {}", redisKey);
        return new IdempotentResult<>(objectMapper.readValue(stored, type), true);
    }

    private void safeDelete(String key) {
        try {
            redis.delete(key);
        } catch (DataAccessException ignored) {
            // Best effort: the IN_PROGRESS marker expires on its own after IN_PROGRESS_TTL.
        }
    }
}
