package com.atakan.blastscale.economy.dto;

import com.atakan.blastscale.economy.BoosterType;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;

public record PurchaseBoosterRequest(@NotNull BoosterType boosterType, @Min(1) @Max(20) int quantity) {
}
