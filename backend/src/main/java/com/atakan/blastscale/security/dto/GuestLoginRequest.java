package com.atakan.blastscale.security.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

/** Guest login uses a stable, client generated device identifier as the identity. */
public record GuestLoginRequest(@NotBlank @Size(min = 8, max = 128) String deviceId) {
}
