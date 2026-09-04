package com.atakan.blastscale.event;

/** <pre>SCHEDULED -> ACTIVE -> ENDED -> FINALIZED</pre> plus CANCELLED from SCHEDULED/ACTIVE. */
public enum LiveEventStatus {
    SCHEDULED,
    ACTIVE,
    ENDED,
    FINALIZED,
    CANCELLED
}
