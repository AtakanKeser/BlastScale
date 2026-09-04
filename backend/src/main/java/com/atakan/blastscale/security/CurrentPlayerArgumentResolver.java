package com.atakan.blastscale.security;

import org.springframework.core.MethodParameter;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken;
import org.springframework.stereotype.Component;
import org.springframework.web.bind.support.WebDataBinderFactory;
import org.springframework.web.context.request.NativeWebRequest;
import org.springframework.web.method.support.HandlerMethodArgumentResolver;
import org.springframework.web.method.support.ModelAndViewContainer;

import java.util.HashSet;
import java.util.List;
import java.util.Set;

/**
 * Turns the JWT in the security context into a {@link PlayerPrincipal} for parameters annotated
 * with {@link CurrentPlayer}.
 *
 * <p>The subject claim holds the numeric player id, {@code username} and {@code roles} are custom
 * claims written by {@link JwtService}. Because everything needed is inside the token, no database
 * round trip is required to authenticate a request — a prerequisite for cheap horizontal scaling.
 */
@Component
public class CurrentPlayerArgumentResolver implements HandlerMethodArgumentResolver {

    @Override
    public boolean supportsParameter(MethodParameter parameter) {
        return parameter.hasParameterAnnotation(CurrentPlayer.class)
                && PlayerPrincipal.class.isAssignableFrom(parameter.getParameterType());
    }

    @Override
    public Object resolveArgument(MethodParameter parameter, ModelAndViewContainer mavContainer,
                                  NativeWebRequest webRequest, WebDataBinderFactory binderFactory) {
        Authentication authentication = SecurityContextHolder.getContext().getAuthentication();
        if (!(authentication instanceof JwtAuthenticationToken jwtAuth)) {
            // Security config already requires authentication on these endpoints;
            // reaching this branch means a misconfiguration, not a client error.
            throw new IllegalStateException("No JWT authentication present");
        }
        return fromJwt(jwtAuth.getToken());
    }

    static PlayerPrincipal fromJwt(Jwt jwt) {
        long playerId = Long.parseLong(jwt.getSubject());
        String username = jwt.getClaimAsString(JwtService.CLAIM_USERNAME);
        List<String> roleClaim = jwt.getClaimAsStringList(JwtService.CLAIM_ROLES);
        Set<String> roles = roleClaim == null ? Set.of() : new HashSet<>(roleClaim);
        return new PlayerPrincipal(playerId, username, roles);
    }
}
