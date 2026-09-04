package com.atakan.blastscale.security.dto;

import java.time.Instant;

public record AuthResponse(String token, Instant expiresAt, long playerId, String username, String role) {
}
