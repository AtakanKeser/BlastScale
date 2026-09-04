package com.atakan.blastscale.security;

import com.atakan.blastscale.player.Player;
import com.atakan.blastscale.player.PlayerService;
import com.atakan.blastscale.security.dto.AuthResponse;
import org.springframework.stereotype.Service;

import java.util.List;

/** Glue between the player module (accounts) and the JWT issuer. */
@Service
public class AuthService {

    private final PlayerService playerService;
    private final JwtService jwtService;

    public AuthService(PlayerService playerService, JwtService jwtService) {
        this.playerService = playerService;
        this.jwtService = jwtService;
    }

    public AuthResponse register(String username, String password) {
        return tokenFor(playerService.register(username, password));
    }

    public AuthResponse login(String username, String password) {
        return tokenFor(playerService.authenticate(username, password));
    }

    public AuthResponse guest(String deviceId) {
        return tokenFor(playerService.loginOrCreateGuest(deviceId));
    }

    private AuthResponse tokenFor(Player player) {
        JwtService.IssuedToken issued = jwtService.issue(player.getId(), player.getUsername(),
                List.of(player.getRole().name()));
        return new AuthResponse(issued.token(), issued.expiresAt(), player.getId(), player.getUsername(),
                player.getRole().name());
    }
}
