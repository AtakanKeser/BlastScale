package com.atakan.blastscale.progression;

import com.atakan.blastscale.progression.dto.ProgressView;
import com.atakan.blastscale.progression.dto.SessionView;
import com.atakan.blastscale.progression.validation.CompletionValidationChain;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

/** Support view of a player's attempts and progress. */
@RestController
@RequestMapping("/api/v1/admin")
@PreAuthorize("hasRole('ADMIN')")
public class ProgressionAdminController {

    private final ProgressionService progressionService;
    private final CompletionValidationChain validationChain;

    public ProgressionAdminController(ProgressionService progressionService, CompletionValidationChain validationChain) {
        this.progressionService = progressionService;
        this.validationChain = validationChain;
    }

    @GetMapping("/players/{playerId}/sessions")
    public List<SessionView> sessions(@PathVariable long playerId, @RequestParam(defaultValue = "20") int limit) {
        return progressionService.recentSessions(playerId, Math.min(limit, 100));
    }

    @GetMapping("/players/{playerId}/progress")
    public ProgressView progress(@PathVariable long playerId) {
        return progressionService.progress(playerId);
    }

    /** The active anti-cheat chain, in execution order (handy on the system page). */
    @GetMapping("/anti-cheat/validators")
    public List<String> validators() {
        return validationChain.validatorNames();
    }
}
