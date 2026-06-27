package com.atakan.blastscale.leaderboard;

import com.atakan.blastscale.leaderboard.dto.LeaderboardView;
import com.atakan.blastscale.security.CurrentPlayer;
import com.atakan.blastscale.security.PlayerPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/v1/leaderboards")
public class LeaderboardController {

    private final LeaderboardService leaderboardService;

    public LeaderboardController(LeaderboardService leaderboardService) {
        this.leaderboardService = leaderboardService;
    }

    /** Top players of the current ISO week plus the caller's own rank. */
    @GetMapping("/weekly")
    public LeaderboardView weekly(@CurrentPlayer PlayerPrincipal principal,
                                  @RequestParam(defaultValue = "100") int limit) {
        return leaderboardService.weekly(principal.playerId(), Math.max(1, Math.min(limit, 100)));
    }
}
