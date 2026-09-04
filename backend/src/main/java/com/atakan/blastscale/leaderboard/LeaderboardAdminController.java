package com.atakan.blastscale.leaderboard;

import com.atakan.blastscale.leaderboard.dto.FinalizationResult;
import com.atakan.blastscale.leaderboard.dto.LeaderboardView;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

/** LiveOps view of any season and a manual finalization trigger. */
@RestController
@RequestMapping("/api/v1/admin/leaderboards")
@PreAuthorize("hasRole('ADMIN')")
public class LeaderboardAdminController {

    private final LeaderboardService leaderboardService;

    public LeaderboardAdminController(LeaderboardService leaderboardService) {
        this.leaderboardService = leaderboardService;
    }

    @GetMapping("/current")
    public LeaderboardView current(@RequestParam(defaultValue = "100") int limit) {
        return leaderboardService.view(leaderboardService.currentSeason(), null, Math.min(limit, 100));
    }

    @GetMapping("/{season}")
    public LeaderboardView season(@PathVariable String season, @RequestParam(defaultValue = "100") int limit) {
        return leaderboardService.view(season, null, Math.min(limit, 100));
    }

    /**
     * Pays the prizes of a season. {@code force=true} allows closing the running season early
     * (used for demos); re-running for an already finalized season is a harmless no-op.
     */
    @PostMapping("/{season}/finalize")
    public FinalizationResult finalizeSeason(@PathVariable String season,
                                             @RequestParam(defaultValue = "false") boolean force) {
        return leaderboardService.finalizeSeason(season, force);
    }
}
