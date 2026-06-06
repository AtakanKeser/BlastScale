package com.atakan.blastscale.experiment;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.type.SqlTypes;

import java.time.Instant;

/**
 * An A/B experiment definition. The variants (names, weights, config overrides) are stored as a
 * JSON document because their shape is free-form and they are never queried relationally.
 */
@Entity
@Table(name = "experiment")
public class Experiment {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    /** Stable identifier used in the bucketing hash, e.g. {@code life_timer_v2}. */
    @Column(name = "experiment_key", nullable = false, length = 64)
    private String key;

    @Column(name = "name", nullable = false, length = 128)
    private String name;

    @Enumerated(EnumType.STRING)
    @Column(name = "status", nullable = false, length = 16)
    private ExperimentStatus status = ExperimentStatus.DRAFT;

    @Column(name = "start_at")
    private Instant startAt;

    @Column(name = "end_at")
    private Instant endAt;

    @JdbcTypeCode(SqlTypes.JSON)
    @Column(name = "variants", nullable = false, columnDefinition = "json")
    private String variantsJson;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt;

    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;

    protected Experiment() {
        // JPA
    }

    public Experiment(String key, String name, String variantsJson, Instant startAt, Instant endAt, Instant now) {
        this.key = key;
        this.name = name;
        this.variantsJson = variantsJson;
        this.startAt = startAt;
        this.endAt = endAt;
        this.createdAt = now;
        this.updatedAt = now;
    }

    public Long getId() {
        return id;
    }

    public String getKey() {
        return key;
    }

    public String getName() {
        return name;
    }

    public ExperimentStatus getStatus() {
        return status;
    }

    public Instant getStartAt() {
        return startAt;
    }

    public Instant getEndAt() {
        return endAt;
    }

    public String getVariantsJson() {
        return variantsJson;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }

    public Instant getUpdatedAt() {
        return updatedAt;
    }

    public void setStatus(ExperimentStatus status, Instant now) {
        this.status = status;
        this.updatedAt = now;
    }

    /** RUNNING and inside the optional [startAt, endAt) window. */
    public boolean isLive(Instant now) {
        return status == ExperimentStatus.RUNNING
                && (startAt == null || !now.isBefore(startAt))
                && (endAt == null || now.isBefore(endAt));
    }
}
