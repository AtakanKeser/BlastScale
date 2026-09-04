package com.atakan.blastscale.event;

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

/** A time-boxed live event with a JSON rule configuration. */
@Entity
@Table(name = "live_event")
public class LiveEvent {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Enumerated(EnumType.STRING)
    @Column(name = "type", nullable = false, length = 32)
    private LiveEventType type;

    @Column(name = "name", nullable = false, length = 128)
    private String name;

    @Column(name = "start_at", nullable = false)
    private Instant startAt;

    @Column(name = "end_at", nullable = false)
    private Instant endAt;

    @JdbcTypeCode(SqlTypes.JSON)
    @Column(name = "configuration", nullable = false, columnDefinition = "json")
    private String configuration;

    @Enumerated(EnumType.STRING)
    @Column(name = "status", nullable = false, length = 16)
    private LiveEventStatus status;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt;

    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;

    protected LiveEvent() {
    }

    public LiveEvent(LiveEventType type, String name, Instant startAt, Instant endAt, String configuration,
                     LiveEventStatus status, Instant now) {
        this.type = type;
        this.name = name;
        this.startAt = startAt;
        this.endAt = endAt;
        this.configuration = configuration;
        this.status = status;
        this.createdAt = now;
        this.updatedAt = now;
    }

    public Long getId() {
        return id;
    }

    public LiveEventType getType() {
        return type;
    }

    public String getName() {
        return name;
    }

    public Instant getStartAt() {
        return startAt;
    }

    public Instant getEndAt() {
        return endAt;
    }

    public String getConfiguration() {
        return configuration;
    }

    public LiveEventStatus getStatus() {
        return status;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }

    public Instant getUpdatedAt() {
        return updatedAt;
    }

    public boolean isActive(Instant now) {
        return status == LiveEventStatus.ACTIVE && !now.isBefore(startAt) && now.isBefore(endAt);
    }

    void transition(LiveEventStatus target, Instant now) {
        this.status = target;
        this.updatedAt = now;
    }

    void setWindow(Instant startAt, Instant endAt, Instant now) {
        this.startAt = startAt;
        this.endAt = endAt;
        this.updatedAt = now;
    }
}
