package com.atakan.blastscale.common.redis;

import org.springframework.boot.context.properties.ConfigurationProperties;

import java.time.Duration;

/** TTLs of the Redis caches, bound from {@code blastscale.cache.*}. */
@ConfigurationProperties(prefix = "blastscale.cache")
public record CacheProperties(
        Duration playerProfileTtl,
        Duration remoteConfigTtl,
        Duration activeEventsTtl,
        Duration levelDefinitionTtl) {
}
