package com.atakan.blastscale.experiment.dto;

import com.atakan.blastscale.experiment.ExperimentVariant;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

import java.time.Instant;
import java.util.List;

public record CreateExperimentRequest(
        @NotBlank @Size(max = 64) @Pattern(regexp = "^[a-z0-9_]+$", message = "lower case letters, digits and underscore")
        String key,
        @NotBlank @Size(max = 128)
        String name,
        @NotEmpty
        List<ExperimentVariant> variants,
        Instant startAt,
        Instant endAt) {
}
