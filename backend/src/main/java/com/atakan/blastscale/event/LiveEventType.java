package com.atakan.blastscale.event;

/**
 * Kinds of live events. Each type has its own {@link EventRule} shape; new types are added by
 * adding an enum constant and a rule record, not by touching gameplay code.
 */
public enum LiveEventType {
    /** Players earn rockets per completed level; the top ranks win coins when the event ends. */
    ROCKET_RACE,
    /** Level rewards are multiplied while the event is active. */
    DOUBLE_REWARD
}
