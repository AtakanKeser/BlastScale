package com.atakan.blastscale.remoteconfig.dto;

import jakarta.validation.constraints.NotNull;

/** Body of {@code PUT /api/v1/admin/config/{key}}; {@code value} may be any JSON value. */
public record UpdateConfigRequest(@NotNull Object value, String description) {
}
