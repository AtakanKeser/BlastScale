package com.atakan.blastscale.event;

import com.atakan.blastscale.event.dto.PlayerEventView;
import com.atakan.blastscale.player.PlayerService;
import com.atakan.blastscale.security.CurrentPlayer;
import com.atakan.blastscale.security.PlayerPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

@RestController
@RequestMapping("/api/v1/events")
public class LiveEventController {

    private final LiveEventService eventService;
    private final PlayerService playerService;

    public LiveEventController(LiveEventService eventService, PlayerService playerService) {
        this.eventService = eventService;
        this.playerService = playerService;
    }

    /** Active events with the caller's points, rank and the current top 10. */
    @GetMapping
    public List<PlayerEventView> active(@CurrentPlayer PlayerPrincipal principal) {
        int level = playerService.getProfile(principal.playerId()).currentLevel();
        return eventService.eventsFor(principal.playerId(), level);
    }
}
