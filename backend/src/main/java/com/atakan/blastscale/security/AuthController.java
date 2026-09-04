package com.atakan.blastscale.security;

import com.atakan.blastscale.security.dto.AuthResponse;
import com.atakan.blastscale.security.dto.GuestLoginRequest;
import com.atakan.blastscale.security.dto.LoginRequest;
import com.atakan.blastscale.security.dto.RegisterRequest;
import jakarta.validation.Valid;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

/**
 * Public authentication endpoints. All three return a bearer token the client sends as
 * {@code Authorization: Bearer <token>} on every other call.
 */
@RestController
@RequestMapping("/api/v1/auth")
public class AuthController {

    private final AuthService authService;

    public AuthController(AuthService authService) {
        this.authService = authService;
    }

    @PostMapping("/register")
    @ResponseStatus(HttpStatus.CREATED)
    public AuthResponse register(@Valid @RequestBody RegisterRequest request) {
        return authService.register(request.username(), request.password());
    }

    @PostMapping("/login")
    public AuthResponse login(@Valid @RequestBody LoginRequest request) {
        return authService.login(request.username(), request.password());
    }

    /** Frictionless mobile onboarding: no password, the device id identifies the player. */
    @PostMapping("/guest")
    public AuthResponse guest(@Valid @RequestBody GuestLoginRequest request) {
        return authService.guest(request.deviceId());
    }
}
