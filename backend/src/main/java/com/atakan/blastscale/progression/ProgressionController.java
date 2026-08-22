package com.atakan.blastscale.progression;

import com.atakan.blastscale.common.TransactionRetry;
import com.atakan.blastscale.common.idempotency.IdempotencyService;
import com.atakan.blastscale.common.idempotency.IdempotentResponses;
import com.atakan.blastscale.common.web.ApiHeaders;
import com.atakan.blastscale.progression.dto.LevelCompleteRequest;
import com.atakan.blastscale.progression.dto.LevelCompleteResponse;
import com.atakan.blastscale.progression.dto.LevelFailRequest;
import com.atakan.blastscale.progression.dto.LevelFailResponse;
import com.atakan.blastscale.progression.dto.LevelStartResponse;
import com.atakan.blastscale.progression.dto.ProgressView;
import com.atakan.blastscale.security.CurrentPlayer;
import com.atakan.blastscale.security.PlayerPrincipal;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/** The gameplay API used by the Unity client. */
@RestController
@RequestMapping("/api/v1")
public class ProgressionController {

    private final ProgressionService progressionService;
    private final IdempotencyService idempotency;

    public ProgressionController(ProgressionService progressionService, IdempotencyService idempotency) {
        this.progressionService = progressionService;
        this.idempotency = idempotency;
    }

    /** Consumes a life and returns the seed + rules of a new attempt. */
    @PostMapping("/levels/{levelId}/start")
    public LevelStartResponse start(@CurrentPlayer PlayerPrincipal principal, @PathVariable int levelId) {
        // The whole transaction is re-run if InnoDB picks it as a deadlock victim.
        return TransactionRetry.run("level-start", () -> progressionService.startLevel(principal.playerId(), levelId));
    }

    /**
     * Reports a won level. Send an {@code Idempotency-Key}: if the response is lost and the request
     * retried, the stored response is returned instead of processing it again.
     */
    @PostMapping("/levels/{levelId}/complete")
    public ResponseEntity<LevelCompleteResponse> complete(
            @CurrentPlayer PlayerPrincipal principal,
            @PathVariable int levelId,
            @Valid @RequestBody LevelCompleteRequest request,
            @RequestHeader(value = ApiHeaders.IDEMPOTENCY_KEY, required = false) String idempotencyKey) {
        long playerId = principal.playerId();
        return IdempotentResponses.of(idempotency.execute("level-complete", playerId, idempotencyKey,
                LevelCompleteResponse.class, () -> progressionService.completeLevel(playerId, levelId, request)));
    }

    /** Reports a lost level (moves are needed to charge used boosters). */
    @PostMapping("/levels/{levelId}/fail")
    public LevelFailResponse fail(@CurrentPlayer PlayerPrincipal principal, @PathVariable int levelId,
                                  @Valid @RequestBody LevelFailRequest request) {
        return progressionService.failLevel(principal.playerId(), levelId, request);
    }

    /** Level map data: current level, stars and best scores. */
    @GetMapping("/progress")
    public ProgressView progress(@CurrentPlayer PlayerPrincipal principal) {
        return progressionService.progress(principal.playerId());
    }
}
