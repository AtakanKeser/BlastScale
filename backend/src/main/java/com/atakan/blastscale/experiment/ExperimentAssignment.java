package com.atakan.blastscale.experiment;

import jakarta.persistence.Column;
import jakarta.persistence.EmbeddedId;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;

import java.time.Instant;

/**
 * Persisted variant assignment.
 *
 * <p>Bucketing is deterministic, so strictly speaking the assignment could be recomputed on every
 * request. It is stored anyway because (1) analytics needs to know exactly who was exposed to
 * which variant and when, and (2) it makes the assignment <b>sticky</b> even if the variant weights
 * are changed while the experiment is running.
 */
@Entity
@Table(name = "experiment_assignment")
public class ExperimentAssignment {

    @EmbeddedId
    private ExperimentAssignmentId id;

    @Column(name = "variant", nullable = false, length = 32)
    private String variant;

    /** The hash bucket (0-99) the player fell into, kept for debugging distribution issues. */
    @Column(name = "bucket", nullable = false)
    private int bucket;

    @Column(name = "assigned_at", nullable = false)
    private Instant assignedAt;

    protected ExperimentAssignment() {
        // JPA
    }

    public ExperimentAssignmentId getId() {
        return id;
    }

    public String getVariant() {
        return variant;
    }

    public int getBucket() {
        return bucket;
    }

    public Instant getAssignedAt() {
        return assignedAt;
    }
}
