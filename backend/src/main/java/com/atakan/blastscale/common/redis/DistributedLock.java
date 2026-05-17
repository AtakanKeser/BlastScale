package com.atakan.blastscale.common.redis;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataAccessException;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.script.DefaultRedisScript;
import org.springframework.stereotype.Component;

import java.time.Duration;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import java.util.function.Supplier;

/**
 * Minimal Redis based distributed lock ({@code SET key token NX PX ttl}).
 *
 * <p>Used only where several API replicas could otherwise do the same work twice:
 * background jobs such as leaderboard / live-event finalization. It is deliberately <b>not</b>
 * used for wallet updates — those are protected by MySQL row locks and unique constraints, which
 * give real transactional guarantees, whereas a Redis lock is only a best-effort coordination tool.
 *
 * <p>The release script compares the stored token before deleting so that an instance whose lock
 * expired cannot release a lock that another instance acquired in the meantime.
 */
@Component
public class DistributedLock {

    private static final Logger log = LoggerFactory.getLogger(DistributedLock.class);

    private static final DefaultRedisScript<Long> RELEASE_SCRIPT = new DefaultRedisScript<>(
            "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end",
            Long.class);

    private final StringRedisTemplate redis;

    public DistributedLock(StringRedisTemplate redis) {
        this.redis = redis;
    }

    /**
     * Runs {@code work} if the lock could be acquired, otherwise returns {@link Optional#empty()}.
     * The lock is always released afterwards, even if the work throws.
     */
    public <T> Optional<T> withLock(String name, Duration ttl, Supplier<T> work) {
        String key = "lock:" + name;
        String token = UUID.randomUUID().toString();
        Boolean acquired;
        try {
            acquired = redis.opsForValue().setIfAbsent(key, token, ttl);
        } catch (DataAccessException e) {
            // Without Redis we cannot coordinate replicas; skipping is the safe choice for jobs,
            // they will simply run on the next schedule tick.
            log.warn("Redis unavailable, cannot acquire lock {}: {}", name, e.getMessage());
            return Optional.empty();
        }
        if (!Boolean.TRUE.equals(acquired)) {
            log.debug("Lock {} is held by another instance, skipping", name);
            return Optional.empty();
        }
        try {
            return Optional.ofNullable(work.get());
        } finally {
            try {
                redis.execute(RELEASE_SCRIPT, List.of(key), token);
            } catch (DataAccessException e) {
                log.warn("Could not release lock {} (it will expire after {}): {}", name, ttl, e.getMessage());
            }
        }
    }
}
