package com.atakan.blastscale.security;

import org.springframework.boot.context.properties.ConfigurationProperties;

/**
 * Rate limiting settings bound from {@code blastscale.rate-limit.*}.
 *
 * @param requestsPerMinute          budget of an authenticated player (keyed by player id)
 * @param anonymousRequestsPerMinute budget of an IP address on the public auth endpoints. Much
 *                                   higher on purpose: mobile carriers put thousands of players
 *                                   behind one NAT address, so a per-IP limit sized for one
 *                                   person would lock whole cities out of the login screen
 */
@ConfigurationProperties(prefix = "blastscale.rate-limit")
public record RateLimitProperties(boolean enabled, int requestsPerMinute, int anonymousRequestsPerMinute) {
}
