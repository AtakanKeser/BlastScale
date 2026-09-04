package com.atakan.blastscale.experiment;

/**
 * Lifecycle of an experiment.
 * <pre> DRAFT -> RUNNING <-> PAUSED -> ENDED </pre>
 * Only RUNNING experiments (inside their optional time window) assign players.
 */
public enum ExperimentStatus {
    DRAFT,
    RUNNING,
    PAUSED,
    ENDED
}
