package com.atakan.blastscale.security;

import org.springframework.boot.context.properties.ConfigurationProperties;

/** Bootstrap admin credentials bound from {@code blastscale.admin.*}. */
@ConfigurationProperties(prefix = "blastscale.admin")
public record AdminProperties(String username, String password) {
}
