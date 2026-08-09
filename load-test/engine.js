/**
 * JavaScript port of the BlastScale puzzle engine
 * (backend/src/main/java/com/atakan/blastscale/level/engine).
 *
 * The server replays every move list it receives on its own copy of the board, so a virtual
 * user can only get a level accepted when its local simulation is bit-for-bit identical to the
 * Java one. Everything below therefore mirrors the Java code line by line:
 *
 *   - SeededRandom : 32-bit LCG, state = (state * 1664525 + 1013904223) mod 2^32,
 *                    nextInt(bound) = (state >>> 8) % bound
 *   - BoardState   : row-major fill, TAP pops the 4-connected group (size >= 2), score += size^2 * 10,
 *                    gravity compacts each column downwards (row 0 is the top), refill top cells
 *                    top-to-bottom column by column, regenerate the board while no pair exists
 *   - simulate     : replay a move list (BoardEngine.simulate)
 *   - greedySolve  : always pop the largest group (GreedySolver.solve)
 *
 * The module is plain ES2015 so it runs unchanged in k6 (Sobek runtime) and in Node
 * (`node verify-engine.mjs` checks it against docs/engine/engine-vectors.json).
 */

/** Marker for a vacated cell while gravity is being applied. */
export const EMPTY = -1;

/** Extra moves granted by the EXTRA_MOVES booster (BoardConfig.EXTRA_MOVES_BONUS). */
export const EXTRA_MOVES_BONUS = 5;

/** Move kinds understood by the engine (MoveType). */
export const MoveType = Object.freeze({ TAP: 'TAP', HAMMER: 'HAMMER', SHUFFLE: 'SHUFFLE' });

/** Points for popping a group of `size` blocks; quadratic to reward big groups. */
export function groupScore(size) {
  return size * size * 10;
}

/** Number of stars (0-3) a score earns according to the level's ascending thresholds. */
export function starsFor(config, score) {
  let stars = 0;
  for (const threshold of config.starThresholds) {
    if (score >= threshold) {
      stars++;
    }
  }
  return stars;
}

/** Builds a TAP move in the exact JSON shape the API expects. */
export function tap(row, col) {
  return { type: MoveType.TAP, row, col };
}

/** Tiny deterministic PRNG: 32-bit linear congruential generator with Numerical Recipes constants. */
export class SeededRandom {
  /** Seeds the generator; the Java code masks the int seed to 32 unsigned bits. */
  constructor(seed) {
    this.state = seed >>> 0;
  }

  /** Advances the state and returns a value in [0, bound); the high bits are used for distribution. */
  nextInt(bound) {
    this.state = (Math.imul(this.state, 1664525) + 1013904223) >>> 0;
    return (this.state >>> 8) % bound;
  }
}

/** Mutable, deterministic board simulation (port of BoardState). */
export class BoardState {
  /** Creates the initial board for a level config and seed, exactly like the server does. */
  constructor(config, seed) {
    this.config = config;
    this.random = new SeededRandom(seed);
    this.cells = [];
    for (let r = 0; r < config.rows; r++) {
      this.cells.push(new Array(config.cols).fill(EMPTY));
    }
    this.score = 0;
    this.movesUsed = 0;
    this.hammersUsed = 0;
    this.shufflesUsed = 0;
    this.fillAll();
    this.ensurePlayable();
  }

  // ------------------------------------------------------------------ queries

  /** Colour index of one cell. */
  cell(row, col) {
    return this.cells[row][col];
  }

  /** Deep copy of the grid (array of rows). */
  snapshot() {
    return this.cells.map((row) => row.slice());
  }

  /** True once the score reached the level target. */
  objectiveReached() {
    return this.score >= this.config.targetScore;
  }

  /** All poppable groups (size >= 2) in scan order; each group starts with its top-left-most cell. */
  groups() {
    const seen = this.newSeen();
    const result = [];
    for (let r = 0; r < this.config.rows; r++) {
      for (let c = 0; c < this.config.cols; c++) {
        if (!seen[r][c]) {
          const group = this.collectGroup(r, c, seen);
          if (group.length >= 2) {
            result.push(group);
          }
        }
      }
    }
    return result;
  }

  // ------------------------------------------------------------------ actions

  /** Applies a move; returns null when it is legal, otherwise a short reason string. */
  apply(move, effectiveMoveLimit) {
    switch (move.type) {
      case MoveType.TAP:
        return this.tap(move.row, move.col, effectiveMoveLimit);
      case MoveType.HAMMER:
        return this.hammer(move.row, move.col);
      case MoveType.SHUFFLE:
        this.shufflesUsed++;
        this.fillAll();
        this.ensurePlayable();
        return null;
      default:
        return 'unknown move type ' + move.type;
    }
  }

  /** Pops the group under (row, col); counts as a move. */
  tap(row, col, effectiveMoveLimit) {
    if (this.movesUsed >= effectiveMoveLimit) {
      return 'move limit exceeded';
    }
    if (!this.inBounds(row, col)) {
      return 'tap out of bounds';
    }
    const group = this.collectGroup(row, col, this.newSeen());
    if (group.length < 2) {
      return 'tapped a single block';
    }
    for (const cell of group) {
      this.cells[cell[0]][cell[1]] = EMPTY;
    }
    this.score += groupScore(group.length);
    this.movesUsed++;
    this.applyGravityAndRefill();
    this.ensurePlayable();
    return null;
  }

  /** Removes a single block (HAMMER booster); does not count as a move. */
  hammer(row, col) {
    if (!this.inBounds(row, col)) {
      return 'hammer out of bounds';
    }
    this.cells[row][col] = EMPTY;
    this.hammersUsed++;
    this.applyGravityAndRefill();
    this.ensurePlayable();
    return null;
  }

  // ------------------------------------------------------------------ mechanics

  /** Fills every cell from the RNG, row by row, left to right. */
  fillAll() {
    for (let r = 0; r < this.config.rows; r++) {
      for (let c = 0; c < this.config.cols; c++) {
        this.cells[r][c] = this.random.nextInt(this.config.colorCount);
      }
    }
  }

  /** Compacts each column downwards and refills the vacated top cells, columns left to right. */
  applyGravityAndRefill() {
    const rows = this.config.rows;
    for (let c = 0; c < this.config.cols; c++) {
      // Compact the column downwards, keeping the relative order of remaining blocks.
      let write = rows - 1;
      for (let r = rows - 1; r >= 0; r--) {
        if (this.cells[r][c] !== EMPTY) {
          this.cells[write][c] = this.cells[r][c];
          write--;
        }
      }
      // Refill the vacated top cells, top to bottom.
      for (let r = 0; r <= write; r++) {
        this.cells[r][c] = EMPTY;
      }
      for (let r = 0; r <= write; r++) {
        this.cells[r][c] = this.random.nextInt(this.config.colorCount);
      }
    }
  }

  /** A board without any group of 2+ is a dead end: regenerate it (at most 100 times). */
  ensurePlayable() {
    let guard = 0;
    while (!this.hasAnyGroup() && guard++ < 100) {
      this.fillAll();
    }
  }

  /** True when any two horizontally or vertically adjacent cells share a colour. */
  hasAnyGroup() {
    const rows = this.config.rows;
    const cols = this.config.cols;
    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        const color = this.cells[r][c];
        if (r + 1 < rows && this.cells[r + 1][c] === color) {
          return true;
        }
        if (c + 1 < cols && this.cells[r][c + 1] === color) {
          return true;
        }
      }
    }
    return false;
  }

  /** Depth-first flood fill of the same-colour 4-connected group containing (row, col). */
  collectGroup(row, col, seen) {
    const group = [];
    const color = this.cells[row][col];
    if (color === EMPTY) {
      return group;
    }
    const stack = [[row, col]];
    seen[row][col] = true;
    while (stack.length > 0) {
      const cur = stack.pop();
      group.push(cur);
      const neighbours = [
        [cur[0] - 1, cur[1]],
        [cur[0] + 1, cur[1]],
        [cur[0], cur[1] - 1],
        [cur[0], cur[1] + 1],
      ];
      for (const n of neighbours) {
        if (this.inBounds(n[0], n[1]) && !seen[n[0]][n[1]] && this.cells[n[0]][n[1]] === color) {
          seen[n[0]][n[1]] = true;
          stack.push(n);
        }
      }
    }
    return group;
  }

  /** Fresh rows x cols matrix of `false` flags for flood fills. */
  newSeen() {
    const seen = [];
    for (let r = 0; r < this.config.rows; r++) {
      seen.push(new Array(this.config.cols).fill(false));
    }
    return seen;
  }

  /** True when (row, col) lies on the board. */
  inBounds(row, col) {
    return row >= 0 && row < this.config.rows && col >= 0 && col < this.config.cols;
  }
}

/**
 * Replays a full move list and reports the outcome (port of BoardEngine.simulate).
 *
 * @returns {{valid: boolean, rejectionReason: (string|null), score: number, movesUsed: number,
 *            hammersUsed: number, shufflesUsed: number, objectiveReached: boolean, stars: number,
 *            board: number[][]}}
 */
export function simulate(config, seed, moves, extraMovesUsed) {
  const state = new BoardState(config, seed);
  const moveLimit = config.moveLimit + (extraMovesUsed ? EXTRA_MOVES_BONUS : 0);
  for (let i = 0; i < moves.length; i++) {
    const problem = state.apply(moves[i], moveLimit);
    if (problem !== null) {
      return {
        valid: false,
        rejectionReason: 'move ' + i + ': ' + problem,
        score: state.score,
        movesUsed: state.movesUsed,
        hammersUsed: state.hammersUsed,
        shufflesUsed: state.shufflesUsed,
        objectiveReached: false,
        stars: 0,
        board: state.snapshot(),
      };
    }
  }
  const reached = state.objectiveReached();
  return {
    valid: true,
    rejectionReason: null,
    score: state.score,
    movesUsed: state.movesUsed,
    hammersUsed: state.hammersUsed,
    shufflesUsed: state.shufflesUsed,
    objectiveReached: reached,
    stars: reached ? starsFor(config, state.score) : 0,
    board: state.snapshot(),
  };
}

/**
 * Simple bot: always pops the largest group until the objective is reached or moves run out
 * (port of GreedySolver.solve). The returned moves are ready to be sent to the API.
 *
 * @returns {{moves: Array<{type: string, row: number, col: number}>, score: number, movesUsed: number,
 *            objectiveReached: boolean, stars: number}}
 */
export function greedySolve(config, seed) {
  const state = new BoardState(config, seed);
  const moves = [];
  while (!state.objectiveReached() && state.movesUsed < config.moveLimit) {
    let best = null;
    for (const group of state.groups()) {
      if (best === null || group.length > best.length) {
        best = group;
      }
    }
    if (best === null) {
      break;
    }
    const move = tap(best[0][0], best[0][1]);
    if (state.apply(move, config.moveLimit) !== null) {
      break;
    }
    moves.push(move);
  }
  const reached = state.objectiveReached();
  return {
    moves,
    score: state.score,
    movesUsed: state.movesUsed,
    objectiveReached: reached,
    stars: reached ? starsFor(config, state.score) : 0,
  };
}
