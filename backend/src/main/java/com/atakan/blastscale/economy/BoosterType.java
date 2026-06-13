package com.atakan.blastscale.economy;

/** Purchasable gameplay helpers; mapped 1:1 to a {@link Resource}. */
public enum BoosterType {
    HAMMER(Resource.BOOSTER_HAMMER),
    SHUFFLE(Resource.BOOSTER_SHUFFLE),
    EXTRA_MOVES(Resource.BOOSTER_EXTRA_MOVES);

    private final Resource resource;

    BoosterType(Resource resource) {
        this.resource = resource;
    }

    public Resource resource() {
        return resource;
    }

    public static BoosterType ofResource(Resource resource) {
        for (BoosterType type : values()) {
            if (type.resource == resource) {
                return type;
            }
        }
        throw new IllegalArgumentException(resource + " is not a booster");
    }
}
