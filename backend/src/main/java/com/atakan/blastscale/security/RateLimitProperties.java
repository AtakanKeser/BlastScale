package com.atakan.blastscale.security;

import org.springframework.boot.context.properties.ConfigurationProperties;

/** Rate limiting settings bound from {@code blastscale.rate-limit.*}. */
@ConfigurationProperties(prefix = "blastscale.rate-limit")
public record RateLimitProperties(boolean enabled, int requestsPerMinute) {
}
