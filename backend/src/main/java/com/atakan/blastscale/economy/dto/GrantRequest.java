package com.atakan.blastscale.economy.dto;

import com.atakan.blastscale.economy.Resource;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

/** Admin grant/removal of resources; {@code amount} may be negative to take resources away. */
public record GrantRequest(@NotNull Resource resource, long amount, @Size(max = 64) String note) {
}
