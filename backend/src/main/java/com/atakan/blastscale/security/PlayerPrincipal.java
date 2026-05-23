package com.atakan.blastscale.security;

import java.util.Set;

/**
 * The authenticated caller, extracted from the JWT claims.
 *
 * <p>Controllers receive it through the {@link CurrentPlayer} annotation, so business code never
 * touches Spring Security internals.
 */
public record PlayerPrincipal(long playerId, String username, Set<String> roles) {

    public boolean isAdmin() {
        return roles.contains("ADMIN");
    }
}
