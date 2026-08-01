package com.atakan.blastscale.remoteconfig;

import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.Map;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

class ResolvedConfigTest {

    @Test
    void fallsBackToDefaultsForMissingKeys() {
        ResolvedConfig cfg = new ResolvedConfig(Map.of(), List.of());
        assertThat(cfg.maxLives()).isEqualTo(5);
        assertThat(cfg.lifeRegenerationMinutes()).isEqualTo(30);
        assertThat(cfg.getBoolean(ConfigKeys.ROCKET_RACE_ENABLED)).isTrue();
        assertThat(cfg.getIntMap(ConfigKeys.BOOSTER_PRICES)).containsEntry("HAMMER", 100);
    }

    @Test
    void databaseValuesAndStringsAreCoerced() {
        ResolvedConfig cfg = new ResolvedConfig(Map.of(
                ConfigKeys.MAX_LIVES, "7",
                ConfigKeys.REWARD_MULTIPLIER, 1.5,
                ConfigKeys.BOOSTER_PRICES, Map.of("HAMMER", "55")), List.of());
        assertThat(cfg.maxLives()).isEqualTo(7);
        assertThat(cfg.getDouble(ConfigKeys.REWARD_MULTIPLIER)).isEqualTo(1.5);
        assertThat(cfg.getIntMap(ConfigKeys.BOOSTER_PRICES)).containsEntry("HAMMER", 55);
        assertThatThrownBy(() -> cfg.getInt("noSuchKey")).isInstanceOf(IllegalArgumentException.class);
    }
}
