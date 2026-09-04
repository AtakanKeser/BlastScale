package com.atakan.blastscale.telemetry;

import org.springframework.boot.context.properties.ConfigurationProperties;

import java.time.Duration;

/** Bound from {@code blastscale.outbox.*}. */
@ConfigurationProperties(prefix = "blastscale.outbox")
public record OutboxProperties(Duration pollInterval, int batchSize, int maxAttempts) {
}
