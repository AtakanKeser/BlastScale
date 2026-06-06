package com.atakan.blastscale.experiment.dto;

import com.atakan.blastscale.experiment.ExperimentVariant;

import java.time.Instant;
import java.util.List;
import java.util.Map;

/** Admin/API representation of an experiment (also the shape cached in Redis). */
public record ExperimentView(
        long id,
        String key,
        String name,
        String status,
        Instant startAt,
        Instant endAt,
        List<ExperimentVariant> variants,
        Map<String, Long> assignments,
        Instant createdAt,
        Instant updatedAt) {
}
