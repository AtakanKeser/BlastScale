package com.atakan.blastscale.experiment;

import jakarta.persistence.Column;
import jakarta.persistence.Embeddable;

import java.io.Serializable;
import java.util.Objects;

/** Composite key: one assignment per (experiment, player). */
@Embeddable
public class ExperimentAssignmentId implements Serializable {

    @Column(name = "experiment_id", nullable = false)
    private Long experimentId;

    @Column(name = "player_id", nullable = false)
    private Long playerId;

    protected ExperimentAssignmentId() {
        // JPA
    }

    public ExperimentAssignmentId(Long experimentId, Long playerId) {
        this.experimentId = experimentId;
        this.playerId = playerId;
    }

    public Long getExperimentId() {
        return experimentId;
    }

    public Long getPlayerId() {
        return playerId;
    }

    @Override
    public boolean equals(Object o) {
        return o instanceof ExperimentAssignmentId other
                && Objects.equals(experimentId, other.experimentId)
                && Objects.equals(playerId, other.playerId);
    }

    @Override
    public int hashCode() {
        return Objects.hash(experimentId, playerId);
    }
}
