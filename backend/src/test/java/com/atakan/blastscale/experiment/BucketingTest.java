package com.atakan.blastscale.experiment;

import com.atakan.blastscale.common.exception.BlastScaleException;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.Map;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

class BucketingTest {

    private static final List<ExperimentVariant> AB = List.of(
            new ExperimentVariant("A", 50, Map.of()),
            new ExperimentVariant("B", 50, Map.of()));

    @Test
    void bucketIsDeterministic() {
        assertThat(Bucketing.bucket(123, "life_timer_v2")).isEqualTo(Bucketing.bucket(123, "life_timer_v2"));
        assertThat(Bucketing.bucket(123, "life_timer_v2")).isBetween(0, 99);
    }

    @Test
    void bucketsAreIndependentAcrossExperiments() {
        int same = 0;
        for (long player = 1; player <= 1000; player++) {
            if (Bucketing.bucket(player, "exp_a") == Bucketing.bucket(player, "exp_b")) {
                same++;
            }
        }
        // ~1% collisions expected for independent uniform hashes; correlated hashes would be far higher
        assertThat(same).isLessThan(50);
    }

    @Test
    void distributionIsRoughlyUniform() {
        int[] counts = new int[Bucketing.BUCKETS];
        int players = 20_000;
        for (long player = 1; player <= players; player++) {
            counts[Bucketing.bucket(player, "uniformity")]++;
        }
        for (int count : counts) {
            assertThat(count).isBetween(140, 260); // expected 200 per bucket
        }
    }

    @Test
    void cumulativeWeightsSelectVariants() {
        assertThat(Bucketing.pick(AB, 0).name()).isEqualTo("A");
        assertThat(Bucketing.pick(AB, 49).name()).isEqualTo("A");
        assertThat(Bucketing.pick(AB, 50).name()).isEqualTo("B");
        assertThat(Bucketing.pick(AB, 99).name()).isEqualTo("B");
        List<ExperimentVariant> uneven = List.of(new ExperimentVariant("control", 90, Map.of()), new ExperimentVariant("treat", 10, Map.of()));
        assertThat(Bucketing.pick(uneven, 89).name()).isEqualTo("control");
        assertThat(Bucketing.pick(uneven, 90).name()).isEqualTo("treat");
    }

    @Test
    void variantValidationRejectsBadDefinitions() {
        assertThatThrownBy(() -> ExperimentService.validateVariants(List.of(new ExperimentVariant("A", 60, Map.of()), new ExperimentVariant("B", 50, Map.of()))))
                .isInstanceOf(BlastScaleException.class).hasMessageContaining("sum to 100");
        assertThatThrownBy(() -> ExperimentService.validateVariants(List.of(new ExperimentVariant("A", 50, Map.of()), new ExperimentVariant("A", 50, Map.of()))))
                .isInstanceOf(BlastScaleException.class).hasMessageContaining("Duplicate");
        ExperimentService.validateVariants(AB); // fine
    }
}
