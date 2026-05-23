package com.atakan.blastscale.security;

import org.springframework.boot.context.properties.ConfigurationProperties;

import java.time.Duration;

/**
 * JWT settings bound from {@code blastscale.jwt.*}.
 *
 * @param secret         HMAC-SHA256 key material; at least 32 bytes, injected from the
 *                       environment in production (never committed)
 * @param issuer         value of the {@code iss} claim, validated on every request
 * @param accessTokenTtl lifetime of an access token
 */
@ConfigurationProperties(prefix = "blastscale.jwt")
public record JwtProperties(String secret, String issuer, Duration accessTokenTtl) {
}
