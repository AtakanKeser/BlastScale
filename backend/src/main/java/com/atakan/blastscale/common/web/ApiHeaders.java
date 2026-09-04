package com.atakan.blastscale.common.web;

/** HTTP header names shared between controllers, the Unity client and the k6 scripts. */
public final class ApiHeaders {

    /** Client generated UUID identifying one logical mutating action (see IdempotencyService). */
    public static final String IDEMPOTENCY_KEY = "Idempotency-Key";

    /** Set to {@code true} on responses that were replayed from the idempotency store. */
    public static final String IDEMPOTENT_REPLAYED = "Idempotent-Replayed";

    private ApiHeaders() {
    }
}
