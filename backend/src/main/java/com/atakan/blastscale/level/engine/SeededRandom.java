package com.atakan.blastscale.level.engine;

/**
 * Tiny deterministic PRNG (32-bit linear congruential generator, Numerical Recipes constants).
 *
 * <p>It is intentionally trivial: the exact same sequence must be reproducible in the Unity
 * client (C#) and in the k6 load-test script (JavaScript) so that server and client agree on every
 * board the seed produces. {@code java.util.Random} would tie the game to the JVM.
 *
 * <pre>
 *   state = (state * 1664525 + 1013904223) mod 2^32
 *   nextInt(bound) = (state >>> 8) % bound        // high bits have the better distribution
 * </pre>
 */
public final class SeededRandom {

    private static final long MASK = 0xFFFFFFFFL;

    private long state;

    public SeededRandom(int seed) {
        this.state = seed & MASK;
    }

    /** @return a value in {@code [0, bound)} */
    public int nextInt(int bound) {
        state = (state * 1664525L + 1013904223L) & MASK;
        return (int) ((state >>> 8) % bound);
    }
}
