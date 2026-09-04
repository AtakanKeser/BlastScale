package com.atakan.blastscale.common.idempotency;

import org.springframework.boot.context.properties.ConfigurationProperties;

import java.time.Duration;

/** Bound from {@code blastscale.idempotency.*}. */
@ConfigurationProperties(prefix = "blastscale.idempotency")
public record IdempotencyProperties(Duration ttl) {
}
