package com.atakan.blastscale.progression;

import org.springframework.boot.context.properties.ConfigurationProperties;

import java.time.Duration;

/**
 * Bound from {@code blastscale.gameplay.*}.
 *
 * @param sessionTtl        a session older than this can no longer be completed
 * @param minMillisPerMove  a completion faster than this per move is physically implausible
 */
@ConfigurationProperties(prefix = "blastscale.gameplay")
public record GameplayProperties(Duration sessionTtl, long minMillisPerMove) {
}
