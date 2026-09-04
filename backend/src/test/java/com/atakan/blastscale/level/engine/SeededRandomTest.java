package com.atakan.blastscale.level.engine;

import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

/** The RNG is the root of determinism between server, Unity client and k6 script. */
class SeededRandomTest {

    @Test
    void producesTheReferenceSequence() {
        // Reference values shared with docs/engine/engine-vectors.json (rng block).
        SeededRandom random = new SeededRandom(12345);
        int[] actual = new int[10];
        for (int i = 0; i < actual.length; i++) {
            actual[i] = random.nextInt(6);
        }
        assertThat(actual).containsExactly(0, 0, 4, 2, 5, 3, 1, 3, 4, 0);
    }

    @Test
    void sameSeedSameSequence() {
        SeededRandom a = new SeededRandom(42);
        SeededRandom b = new SeededRandom(42);
        for (int i = 0; i < 1000; i++) {
            assertThat(a.nextInt(7)).isEqualTo(b.nextInt(7));
        }
    }

    @Test
    void staysInsideBound() {
        SeededRandom random = new SeededRandom(Integer.MAX_VALUE);
        for (int i = 0; i < 10_000; i++) {
            assertThat(random.nextInt(5)).isBetween(0, 4);
        }
    }
}
