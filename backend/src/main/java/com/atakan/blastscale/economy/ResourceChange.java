package com.atakan.blastscale.economy;

/** One balance movement: positive = credit, negative = debit. */
public record ResourceChange(Resource resource, long amount) {

    public static ResourceChange credit(Resource resource, long amount) {
        return new ResourceChange(resource, Math.abs(amount));
    }

    public static ResourceChange debit(Resource resource, long amount) {
        return new ResourceChange(resource, -Math.abs(amount));
    }
}
