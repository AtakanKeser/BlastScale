package com.atakan.blastscale.level.engine;

import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Deque;
import java.util.List;

/**
 * Mutable, deterministic board simulation. This is the whole "game": the Unity client runs an
 * identical copy for rendering and the server replays the recorded moves to decide the outcome.
 *
 * <p>Conventions (must match the C# and JS ports exactly):
 * <ul>
 *   <li>cells are filled row by row, left to right, from the seeded RNG;</li>
 *   <li>a TAP pops the 4-connected group (size >= 2) containing the tapped cell;</li>
 *   <li>gravity compacts each column downwards (row 0 is the top);</li>
 *   <li>refill happens column by column (left to right), empty cells top to bottom;</li>
 *   <li>if no group of size >= 2 exists after a move, the board is regenerated from the RNG.</li>
 * </ul>
 */
public final class BoardState {

    private static final int EMPTY = -1;

    private final BoardConfig config;
    private final SeededRandom random;
    private final int[][] cells;
    private int score;
    private int movesUsed;
    private int hammersUsed;
    private int shufflesUsed;

    public BoardState(BoardConfig config, int seed) {
        this.config = config;
        this.random = new SeededRandom(seed);
        this.cells = new int[config.rows()][config.cols()];
        fillAll();
        ensurePlayable();
    }

    // ------------------------------------------------------------------ queries

    public int score() {
        return score;
    }

    public int movesUsed() {
        return movesUsed;
    }

    public int hammersUsed() {
        return hammersUsed;
    }

    public int shufflesUsed() {
        return shufflesUsed;
    }

    public int cell(int row, int col) {
        return cells[row][col];
    }

    public int[][] snapshot() {
        int[][] copy = new int[config.rows()][];
        for (int r = 0; r < config.rows(); r++) {
            copy[r] = cells[r].clone();
        }
        return copy;
    }

    public boolean objectiveReached() {
        return score >= config.targetScore();
    }

    /** All poppable groups (size >= 2); used by solvers/tests, not by validation. */
    public List<List<int[]>> groups() {
        boolean[][] seen = new boolean[config.rows()][config.cols()];
        List<List<int[]>> result = new ArrayList<>();
        for (int r = 0; r < config.rows(); r++) {
            for (int c = 0; c < config.cols(); c++) {
                if (!seen[r][c]) {
                    List<int[]> group = collectGroup(r, c, seen);
                    if (group.size() >= 2) {
                        result.add(group);
                    }
                }
            }
        }
        return result;
    }

    // ------------------------------------------------------------------ actions

    /**
     * Applies a move.
     *
     * @return {@code null} when the move is legal, otherwise a short reason string
     */
    public String apply(Move move, int effectiveMoveLimit) {
        return switch (move.type()) {
            case TAP -> tap(move.row(), move.col(), effectiveMoveLimit);
            case HAMMER -> hammer(move.row(), move.col());
            case SHUFFLE -> {
                shufflesUsed++;
                fillAll();
                ensurePlayable();
                yield null;
            }
        };
    }

    private String tap(int row, int col, int effectiveMoveLimit) {
        if (movesUsed >= effectiveMoveLimit) {
            return "move limit exceeded";
        }
        if (!inBounds(row, col)) {
            return "tap out of bounds";
        }
        List<int[]> group = collectGroup(row, col, new boolean[config.rows()][config.cols()]);
        if (group.size() < 2) {
            return "tapped a single block";
        }
        for (int[] cell : group) {
            cells[cell[0]][cell[1]] = EMPTY;
        }
        score += BoardConfig.groupScore(group.size());
        movesUsed++;
        applyGravityAndRefill();
        ensurePlayable();
        return null;
    }

    private String hammer(int row, int col) {
        if (!inBounds(row, col)) {
            return "hammer out of bounds";
        }
        cells[row][col] = EMPTY;
        hammersUsed++;
        applyGravityAndRefill();
        ensurePlayable();
        return null;
    }

    // ------------------------------------------------------------------ mechanics

    private void fillAll() {
        for (int r = 0; r < config.rows(); r++) {
            for (int c = 0; c < config.cols(); c++) {
                cells[r][c] = random.nextInt(config.colorCount());
            }
        }
    }

    private void applyGravityAndRefill() {
        for (int c = 0; c < config.cols(); c++) {
            // Compact the column downwards, keeping the relative order of remaining blocks.
            int write = config.rows() - 1;
            for (int r = config.rows() - 1; r >= 0; r--) {
                if (cells[r][c] != EMPTY) {
                    cells[write][c] = cells[r][c];
                    write--;
                }
            }
            // Refill the vacated top cells, top to bottom.
            for (int r = 0; r <= write; r++) {
                cells[r][c] = EMPTY;
            }
            for (int r = 0; r <= write; r++) {
                cells[r][c] = random.nextInt(config.colorCount());
            }
        }
    }

    /** A board without any group of 2+ would be a dead end: regenerate until it is playable. */
    private void ensurePlayable() {
        int guard = 0;
        while (!hasAnyGroup() && guard++ < 100) {
            fillAll();
        }
    }

    private boolean hasAnyGroup() {
        for (int r = 0; r < config.rows(); r++) {
            for (int c = 0; c < config.cols(); c++) {
                int color = cells[r][c];
                if (r + 1 < config.rows() && cells[r + 1][c] == color) {
                    return true;
                }
                if (c + 1 < config.cols() && cells[r][c + 1] == color) {
                    return true;
                }
            }
        }
        return false;
    }

    private List<int[]> collectGroup(int row, int col, boolean[][] seen) {
        List<int[]> group = new ArrayList<>();
        int color = cells[row][col];
        if (color == EMPTY) {
            return group;
        }
        Deque<int[]> stack = new ArrayDeque<>();
        stack.push(new int[]{row, col});
        seen[row][col] = true;
        while (!stack.isEmpty()) {
            int[] cur = stack.pop();
            group.add(cur);
            int[][] neighbours = {{cur[0] - 1, cur[1]}, {cur[0] + 1, cur[1]}, {cur[0], cur[1] - 1}, {cur[0], cur[1] + 1}};
            for (int[] n : neighbours) {
                if (inBounds(n[0], n[1]) && !seen[n[0]][n[1]] && cells[n[0]][n[1]] == color) {
                    seen[n[0]][n[1]] = true;
                    stack.push(n);
                }
            }
        }
        return group;
    }

    private boolean inBounds(int row, int col) {
        return row >= 0 && row < config.rows() && col >= 0 && col < config.cols();
    }
}
