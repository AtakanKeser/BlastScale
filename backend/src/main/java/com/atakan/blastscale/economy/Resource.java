package com.atakan.blastscale.economy;

/** Everything a player can own. Each ledger entry moves exactly one of these. */
public enum Resource {
    COIN,
    LIFE,
    STAR,
    BOOSTER_HAMMER,
    BOOSTER_SHUFFLE,
    BOOSTER_EXTRA_MOVES;

    public boolean isBooster() {
        return name().startsWith("BOOSTER_");
    }
}
