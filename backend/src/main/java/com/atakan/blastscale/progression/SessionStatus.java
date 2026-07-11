package com.atakan.blastscale.progression;

/**
 * <pre>ACTIVE -> COMPLETED | FAILED | ABANDONED (a new level was started) | EXPIRED (TTL job)</pre>
 * Only ACTIVE sessions can be completed, and the transition is a conditional UPDATE, so exactly
 * one completion request can ever win.
 */
public enum SessionStatus {
    ACTIVE,
    COMPLETED,
    FAILED,
    ABANDONED,
    EXPIRED
}
