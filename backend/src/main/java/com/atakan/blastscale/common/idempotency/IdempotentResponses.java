package com.atakan.blastscale.common.idempotency;

import com.atakan.blastscale.common.web.ApiHeaders;
import org.springframework.http.ResponseEntity;

/** Wraps an {@link IdempotentResult} in a response that flags replays with a header. */
public final class IdempotentResponses {

    private IdempotentResponses() {
    }

    public static <T> ResponseEntity<T> of(IdempotentResult<T> result) {
        return ResponseEntity.ok()
                .header(ApiHeaders.IDEMPOTENT_REPLAYED, Boolean.toString(result.replayed()))
                .body(result.value());
    }
}
