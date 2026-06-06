package com.atakan.blastscale.remoteconfig.dto;

import java.time.Instant;

public record ConfigEntryView(String key, Object value, String description, Instant updatedAt, String updatedBy) {
}
