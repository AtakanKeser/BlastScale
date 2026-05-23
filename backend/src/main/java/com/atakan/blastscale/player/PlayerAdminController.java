package com.atakan.blastscale.player;

import org.springframework.data.domain.Page;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import java.time.Instant;
import java.util.List;

/** Player lookup for the LiveOps / support panel. */
@RestController
@RequestMapping("/api/v1/admin/players")
@PreAuthorize("hasRole('ADMIN')")
public class PlayerAdminController {

    private final PlayerService playerService;

    public PlayerAdminController(PlayerService playerService) {
        this.playerService = playerService;
    }

    @GetMapping
    public PagedPlayers search(@RequestParam(defaultValue = "") String query,
                               @RequestParam(defaultValue = "0") int page,
                               @RequestParam(defaultValue = "20") int size) {
        Page<Player> result = playerService.search(query, page, Math.min(size, 100));
        List<PlayerRow> rows = result.getContent().stream()
                .map(p -> new PlayerRow(p.getId(), p.getUsername(), p.getRole().name(), p.getCurrentLevel(),
                        p.getDeviceId() != null, p.getCreatedAt(), p.getLastSeenAt()))
                .toList();
        return new PagedPlayers(rows, result.getTotalElements(), page, result.getSize());
    }

    /** Full profile, bypassing the cache so support always sees the current state. */
    @GetMapping("/{playerId}")
    public PlayerProfile get(@PathVariable long playerId) {
        return playerService.loadProfile(playerId);
    }

    public record PlayerRow(long id, String username, String role, int currentLevel, boolean guest,
                            Instant createdAt, Instant lastSeenAt) {
    }

    public record PagedPlayers(List<PlayerRow> players, long total, int page, int size) {
    }
}
