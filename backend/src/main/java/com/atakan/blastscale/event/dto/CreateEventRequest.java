package com.atakan.blastscale.event.dto;

import com.atakan.blastscale.event.LiveEventType;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

import java.time.Instant;
import java.util.Map;

/**
 * @param startAt       {@code null} = start immediately
 * @param endAt         required, must be after startAt
 * @param configuration type specific rule JSON, e.g. {@code {"pointsPerLevel":1,"minimumLevel":5,"rewards":{"1":10000}}}
 */
public record CreateEventRequest(
        @NotNull LiveEventType type,
        @NotBlank @Size(max = 128) String name,
        Instant startAt,
        @NotNull Instant endAt,
        Map<String, Object> configuration) {
}
