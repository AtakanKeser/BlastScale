package com.atakan.blastscale.security;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.player.PlayerRepository;
import com.atakan.blastscale.player.PlayerRole;
import com.atakan.blastscale.player.PlayerService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.stereotype.Component;

/**
 * Creates the admin account on first start so the LiveOps panel is usable immediately after
 * {@code docker compose up}. Safe with several replicas starting at once: the username unique
 * index makes the second creation fail, which is simply ignored.
 */
@Component
public class AdminBootstrap implements ApplicationRunner {

    private static final Logger log = LoggerFactory.getLogger(AdminBootstrap.class);

    private final PlayerRepository players;
    private final PlayerService playerService;
    private final AdminProperties properties;

    public AdminBootstrap(PlayerRepository players, PlayerService playerService, AdminProperties properties) {
        this.players = players;
        this.playerService = playerService;
        this.properties = properties;
    }

    @Override
    public void run(ApplicationArguments args) {
        if (players.existsByUsername(properties.username())) {
            return;
        }
        try {
            playerService.register(properties.username(), properties.password(), PlayerRole.ADMIN);
            log.info("Created bootstrap admin account '{}'", properties.username());
        } catch (BlastScaleException e) {
            log.debug("Admin account already created by another instance: {}", e.getMessage());
        }
    }
}
