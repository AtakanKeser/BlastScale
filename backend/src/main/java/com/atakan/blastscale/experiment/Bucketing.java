package com.atakan.blastscale.experiment;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.List;

/**
 * Deterministic bucketing: {@code hash(playerId + ":" + experimentKey) % 100}.
 *
 * <p>Properties that matter for experimentation:
 * <ul>
 *   <li><b>Sticky</b>: the same player always lands in the same bucket for a given experiment, on
 *       every replica, with no shared state.</li>
 *   <li><b>Independent across experiments</b>: the key is part of the hash, so being in variant B
 *       of one experiment says nothing about the bucket in another one.</li>
 *   <li><b>Uniform</b>: SHA-256 spreads sequential player ids evenly, unlike a plain modulo.</li>
 * </ul>
 */
final class Bucketing {

    static final int BUCKETS = 100;

    private Bucketing() {
    }

    static int bucket(long playerId, String experimentKey) {
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            byte[] hash = digest.digest((playerId + ":" + experimentKey).getBytes(StandardCharsets.UTF_8));
            int value = ((hash[0] & 0xff) << 24) | ((hash[1] & 0xff) << 16) | ((hash[2] & 0xff) << 8) | (hash[3] & 0xff);
            return Math.floorMod(value, BUCKETS);
        } catch (NoSuchAlgorithmException e) {
            throw new IllegalStateException("SHA-256 not available", e);
        }
    }

    /** Walks the cumulative weights: bucket 0-49 -> first 50% variant, 50-99 -> next, ... */
    static ExperimentVariant pick(List<ExperimentVariant> variants, int bucket) {
        int cumulative = 0;
        for (ExperimentVariant variant : variants) {
            cumulative += variant.weight();
            if (bucket < cumulative) {
                return variant;
            }
        }
        return variants.get(variants.size() - 1);
    }
}
