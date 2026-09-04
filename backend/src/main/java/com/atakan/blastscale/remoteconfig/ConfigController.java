package com.atakan.blastscale.remoteconfig;

import com.atakan.blastscale.remoteconfig.dto.ClientConfigResponse;
import com.atakan.blastscale.security.CurrentPlayer;
import com.atakan.blastscale.security.PlayerPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.time.Clock;
import java.time.Instant;

/** Player-facing configuration endpoint, called once when the game starts. */
@RestController
@RequestMapping("/api/v1/config")
public class ConfigController {

    private final RemoteConfigService remoteConfigService;
    private final Clock clock;

    public ConfigController(RemoteConfigService remoteConfigService, Clock clock) {
        this.remoteConfigService = remoteConfigService;
        this.clock = clock;
    }

    @GetMapping
    public ClientConfigResponse config(@CurrentPlayer PlayerPrincipal principal) {
        ResolvedConfig resolved = remoteConfigService.resolveFor(principal.playerId());
        return new ClientConfigResponse(
                resolved.values(),
                resolved.experiments().stream()
                        .map(a -> new ClientConfigResponse.ExperimentAssignmentView(a.experimentId(), a.key(), a.variant()))
                        .toList(),
                Instant.now(clock));
    }
}
