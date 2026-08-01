package com.atakan.blastscale.level;

import com.atakan.blastscale.level.engine.BoardConfig;
import com.atakan.blastscale.level.engine.BoardEngine;
import com.atakan.blastscale.level.engine.GreedySolver;
import com.atakan.blastscale.level.engine.Move;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

import java.time.Instant;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

/** Difficulty calibration: the greedy bot must be able to clear generated levels most of the time. */
class ProceduralLevelGeneratorTest {

    @ParameterizedTest
    @ValueSource(ints = {1, 5, 10, 20, 30, 50, 80, 150})
    void greedyBotClearsMostLevels(int level) {
        BoardConfig config = ProceduralLevelGenerator.generate(level, Instant.now()).toBoardConfig();
        int wins = 0;
        int seeds = 60;
        for (int seed = 1; seed <= seeds; seed++) {
            List<Move> moves = GreedySolver.solve(config, seed * 31);
            if (BoardEngine.simulate(config, seed * 31, moves, false).objectiveReached()) {
                wins++;
            }
        }
        double winRate = wins / (double) seeds;
        assertThat(winRate).as("level %d win rate", level).isGreaterThanOrEqualTo(level <= 10 ? 0.95 : 0.6);
    }

    @Test
    void difficultyRampsWithLevel() {
        LevelDefinition early = ProceduralLevelGenerator.generate(1, Instant.now());
        LevelDefinition late = ProceduralLevelGenerator.generate(60, Instant.now());
        assertThat(early.getColorCount()).isLessThan(late.getColorCount());
        assertThat(early.getMoveLimit()).isGreaterThan(late.getMoveLimit());
        assertThat(early.getStarThresholds()).hasSize(3).isSorted();
        assertThat(early.getStarThresholds().get(0)).isEqualTo(early.getTargetScore());
        assertThat(early.getId()).isEqualTo("level-1");
    }
}
