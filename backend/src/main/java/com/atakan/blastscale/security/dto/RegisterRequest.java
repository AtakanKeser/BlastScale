package com.atakan.blastscale.security.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

public record RegisterRequest(
        @NotBlank @Size(min = 3, max = 32)
        @Pattern(regexp = "^[a-zA-Z0-9_]+$", message = "only letters, digits and underscore are allowed")
        String username,
        @NotBlank @Size(min = 8, max = 72)
        String password) {
}
