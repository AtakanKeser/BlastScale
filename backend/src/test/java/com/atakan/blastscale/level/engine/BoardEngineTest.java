package com.atakan.blastscale.level.engine;

import org.junit.jupiter.api.Test;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.io.InputStream;
import java.util.ArrayList;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

/** Engine rules and the golden vectors that the client ports are checked against. */
class BoardEngineTest {

    private static final BoardConfig CONFIG = new BoardConfig(8, 8, 4, 20, 1790, List.of(1790, 2238, 2685));

    @Test
    void boardsAreDeterministicPerSeed() {
        assertThat(new BoardState(CONFIG, 99).snapshot()).isDeepEqualTo(new BoardState(CONFIG, 99).snapshot());
        assertThat(new BoardState(CONFIG, 99).snapshot()).isNotEqualTo(new BoardState(CONFIG, 100).snapshot());
    }

    @Test
    void freshBoardIsAlwaysPlayable() {
        for (int seed = 1; seed < 500; seed++) {
            assertThat(new BoardState(CONFIG, seed).groups()).as("seed %d", seed).isNotEmpty();
        }
    }

    @Test
    void tapScoresQuadraticallyAndCountsAMove() {
        BoardState state = new BoardState(CONFIG, 7);
        List<int[]> group = state.groups().get(0);
        int[] cell = group.get(0);
        assertThat(state.apply(Move.tap(cell[0], cell[1]), CONFIG.moveLimit())).isNull();
        assertThat(state.score()).isEqualTo(BoardConfig.groupScore(group.size()));
        assertThat(state.movesUsed()).isEqualTo(1);
    }

    @Test
    void tappingASingleBlockIsIllegal() {
        BoardState state = new BoardState(CONFIG, 777);
        int[][] board = state.snapshot();
        for (int r = 0; r < 8; r++) {
            for (int c = 0; c < 8; c++) {
                if (isAlone(board, r, c)) {
                    assertThat(state.apply(Move.tap(r, c), CONFIG.moveLimit())).isEqualTo("tapped a single block");
                    return;
                }
            }
        }
    }

    @Test
    void outOfBoundsAndOverLimitAreIllegal() {
        BoardState state = new BoardState(CONFIG, 3);
        assertThat(state.apply(Move.tap(8, 0), CONFIG.moveLimit())).isEqualTo("tap out of bounds");
        assertThat(state.apply(Move.tap(0, 0), 0)).isEqualTo("move limit exceeded");
    }

    @Test
    void gravityLeavesNoHolesAndKeepsColumnOrder() {
        BoardState state = new BoardState(CONFIG, 5);
        int[][] before = state.snapshot();
        List<int[]> group = state.groups().get(0);
        state.apply(Move.tap(group.get(0)[0], group.get(0)[1]), CONFIG.moveLimit());
        int[][] after = state.snapshot();
        for (int c = 0; c < 8; c++) {
            // survivors of the column keep their relative order, bottom aligned
            List<Integer> survivors = new ArrayList<>();
            for (int r = 0; r < 8; r++) {
                final int row = r;
                final int col = c;
                boolean removed = group.stream().anyMatch(g -> g[1] == col && g[0] == row);
                if (!removed) {
                    survivors.add(before[r][c]);
                }
            }
            for (int i = 0; i < survivors.size(); i++) {
                int row = 8 - survivors.size() + i;
                assertThat(after[row][c]).isEqualTo(survivors.get(i));
            }
            for (int r = 0; r < 8; r++) {
                assertThat(after[r][c]).isBetween(0, 3);
            }
        }
    }

    @Test
    void extraMovesBoosterRaisesTheLimit() {
        List<Move> moves = new ArrayList<>();
        BoardState probe = new BoardState(CONFIG, 11);
        // play 22 legal taps: more than the limit of 20 but within 20 + 5
        while (moves.size() < 22) {
            List<int[]> g = probe.groups().get(0);
            Move m = Move.tap(g.get(0)[0], g.get(0)[1]);
            probe.apply(m, 100);
            moves.add(m);
        }
        assertThat(BoardEngine.simulate(CONFIG, 11, moves, false).valid()).isFalse();
        assertThat(BoardEngine.simulate(CONFIG, 11, moves, true).valid()).isTrue();
    }

    @Test
    void starsFollowThresholds() {
        assertThat(CONFIG.starsFor(1789)).isZero();
        assertThat(CONFIG.starsFor(1790)).isEqualTo(1);
        assertThat(CONFIG.starsFor(2238)).isEqualTo(2);
        assertThat(CONFIG.starsFor(9999)).isEqualTo(3);
    }

    /** Golden test: every case in the shared vector file must replay identically. */
    @Test
    void matchesTheSharedEngineVectors() throws Exception {
        ObjectMapper mapper = new ObjectMapper();
        JsonNode root;
        try (InputStream in = getClass().getResourceAsStream("/engine-vectors.json")) {
            root = mapper.readTree(in);
        }
        int checked = 0;
        for (JsonNode c : root.get("cases")) {
            JsonNode cfg = c.get("config");
            List<Integer> thresholds = new ArrayList<>();
            cfg.get("starThresholds").forEach(n -> thresholds.add(n.asInt()));
            BoardConfig config = new BoardConfig(cfg.get("rows").asInt(), cfg.get("cols").asInt(), cfg.get("colorCount").asInt(),
                    cfg.get("moveLimit").asInt(), cfg.get("targetScore").asInt(), thresholds);
            int seed = c.get("seed").asInt();
            assertThat(new BoardState(config, seed).snapshot()).isDeepEqualTo(board(c.get("initialBoard")));

            List<Move> moves = new ArrayList<>();
            c.get("moves").forEach(m -> moves.add(new Move(MoveType.valueOf(m.get("type").asText()), m.get("row").asInt(), m.get("col").asInt())));
            SimulationResult result = BoardEngine.simulate(config, seed, moves, c.get("extraMovesUsed").asBoolean());
            assertThat(result.valid()).isEqualTo(c.get("valid").asBoolean());
            assertThat(result.score()).isEqualTo(c.get("finalScore").asInt());
            assertThat(result.movesUsed()).isEqualTo(c.get("finalMovesUsed").asInt());
            assertThat(result.hammersUsed()).isEqualTo(c.get("hammersUsed").asInt());
            assertThat(result.shufflesUsed()).isEqualTo(c.get("shufflesUsed").asInt());
            assertThat(result.objectiveReached()).isEqualTo(c.get("objectiveReached").asBoolean());
            assertThat(result.stars()).isEqualTo(c.get("stars").asInt());
            checked++;
        }
        assertThat(checked).isEqualTo(root.get("cases").size());
    }

    private static int[][] board(JsonNode node) {
        int[][] board = new int[node.size()][];
        for (int r = 0; r < node.size(); r++) {
            board[r] = new int[node.get(r).size()];
            for (int c = 0; c < node.get(r).size(); c++) {
                board[r][c] = node.get(r).get(c).asInt();
            }
        }
        return board;
    }

    private static boolean isAlone(int[][] b, int r, int c) {
        int color = b[r][c];
        return (r == 0 || b[r - 1][c] != color) && (r == 7 || b[r + 1][c] != color)
                && (c == 0 || b[r][c - 1] != color) && (c == 7 || b[r][c + 1] != color);
    }
}
