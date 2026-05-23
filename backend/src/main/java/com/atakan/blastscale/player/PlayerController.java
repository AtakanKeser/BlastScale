package com.atakan.blastscale.player;

import com.atakan.blastscale.security.CurrentPlayer;
import com.atakan.blastscale.security.PlayerPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/** Player-facing endpoints. */
@RestController
@RequestMapping("/api/v1/players")
public class PlayerController {

    private final PlayerService playerService;

    public PlayerController(PlayerService playerService) {
        this.playerService = playerService;
    }

    /** The caller's own profile (cached in Redis, see {@link PlayerService#getProfile}). */
    @GetMapping("/me")
    public PlayerProfile me(@CurrentPlayer PlayerPrincipal principal) {
        return playerService.getProfile(principal.playerId());
    }
}
