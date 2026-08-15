using System.Collections.Generic;

namespace BlastScale.Engine
{
    /// <summary>
    /// Mutable, deterministic board simulation (port of <c>BoardState.java</c>). This is the whole
    /// "game": the client runs it for rendering and the server replays the recorded moves on an
    /// identical copy to decide the outcome — so every rule below must match the Java code exactly.
    ///
    /// Conventions shared by all ports:
    /// <list type="bullet">
    ///   <item>cells are filled row by row, left to right, from the seeded RNG;</item>
    ///   <item>a TAP pops the 4-connected group (size >= 2) containing the tapped cell;</item>
    ///   <item>gravity compacts each column downwards (row 0 is the top);</item>
    ///   <item>refill happens column by column (left to right), empty cells top to bottom;</item>
    ///   <item>if no group of size >= 2 exists after a move, the board is regenerated from the RNG.</item>
    /// </list>
    /// </summary>
    public sealed class BoardState
    {
        private const int Empty = -1;

        private readonly BoardConfig _config;
        private readonly SeededRandom _random;
        private readonly int[][] _cells;

        public int Score { get; private set; }
        public int MovesUsed { get; private set; }
        public int HammersUsed { get; private set; }
        public int ShufflesUsed { get; private set; }

        public BoardConfig Config => _config;
        public int Rows => _config.Rows;
        public int Cols => _config.Cols;

        /// <summary>Creates the initial board for a seed: fill everything, then make sure it is playable.</summary>
        public BoardState(BoardConfig config, int seed)
        {
            _config = config;
            _random = new SeededRandom(seed);
            _cells = new int[config.Rows][];
            for (int r = 0; r < config.Rows; r++)
            {
                _cells[r] = new int[config.Cols];
            }
            FillAll();
            EnsurePlayable();
        }

        // ------------------------------------------------------------------ queries

        public int Cell(int row, int col)
        {
            return _cells[row][col];
        }

        /// <summary>Deep copy of the grid (row-major), used by tests and the renderer.</summary>
        public int[][] Snapshot()
        {
            var copy = new int[_config.Rows][];
            for (int r = 0; r < _config.Rows; r++)
            {
                copy[r] = (int[])_cells[r].Clone();
            }
            return copy;
        }

        public bool ObjectiveReached => Score >= _config.TargetScore;

        /// <summary>All poppable groups (size >= 2); used by solvers/tests/UI hints, not by validation.</summary>
        public List<List<CellPos>> Groups()
        {
            var seen = NewSeenGrid();
            var result = new List<List<CellPos>>();
            for (int r = 0; r < _config.Rows; r++)
            {
                for (int c = 0; c < _config.Cols; c++)
                {
                    if (!seen[r][c])
                    {
                        List<CellPos> group = CollectGroup(r, c, seen);
                        if (group.Count >= 2)
                        {
                            result.Add(group);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// The group a TAP at this cell would pop (possibly a single cell, i.e. an illegal tap).
        /// Read-only helper for the UI; it does not change any state.
        /// </summary>
        public List<CellPos> GroupAt(int row, int col)
        {
            if (!InBounds(row, col))
            {
                return new List<CellPos>();
            }
            return CollectGroup(row, col, NewSeenGrid());
        }

        // ------------------------------------------------------------------ actions

        /// <summary>
        /// Applies a move.
        /// </summary>
        /// <returns><c>null</c> when the move is legal, otherwise a short reason string</returns>
        public string Apply(Move move, int effectiveMoveLimit)
        {
            switch (move.Type)
            {
                case MoveType.TAP:
                    return Tap(move.Row, move.Col, effectiveMoveLimit);
                case MoveType.HAMMER:
                    return Hammer(move.Row, move.Col);
                case MoveType.SHUFFLE:
                    ShufflesUsed++;
                    FillAll();
                    EnsurePlayable();
                    return null;
                default:
                    return "unknown move type";
            }
        }

        private string Tap(int row, int col, int effectiveMoveLimit)
        {
            if (MovesUsed >= effectiveMoveLimit)
            {
                return "move limit exceeded";
            }
            if (!InBounds(row, col))
            {
                return "tap out of bounds";
            }
            List<CellPos> group = CollectGroup(row, col, NewSeenGrid());
            if (group.Count < 2)
            {
                return "tapped a single block";
            }
            foreach (CellPos cell in group)
            {
                _cells[cell.Row][cell.Col] = Empty;
            }
            Score += BoardConfig.GroupScore(group.Count);
            MovesUsed++;
            ApplyGravityAndRefill();
            EnsurePlayable();
            return null;
        }

        private string Hammer(int row, int col)
        {
            if (!InBounds(row, col))
            {
                return "hammer out of bounds";
            }
            _cells[row][col] = Empty;
            HammersUsed++;
            ApplyGravityAndRefill();
            EnsurePlayable();
            return null;
        }

        // ------------------------------------------------------------------ mechanics

        /// <summary>Row-major fill from the RNG; the order is part of the contract.</summary>
        private void FillAll()
        {
            for (int r = 0; r < _config.Rows; r++)
            {
                for (int c = 0; c < _config.Cols; c++)
                {
                    _cells[r][c] = _random.NextInt(_config.ColorCount);
                }
            }
        }

        /// <summary>Gravity per column (bottom-up, order preserved) followed by a top-to-bottom refill.</summary>
        private void ApplyGravityAndRefill()
        {
            for (int c = 0; c < _config.Cols; c++)
            {
                // Compact the column downwards, keeping the relative order of remaining blocks.
                int write = _config.Rows - 1;
                for (int r = _config.Rows - 1; r >= 0; r--)
                {
                    if (_cells[r][c] != Empty)
                    {
                        _cells[write][c] = _cells[r][c];
                        write--;
                    }
                }
                // Refill the vacated top cells, top to bottom.
                for (int r = 0; r <= write; r++)
                {
                    _cells[r][c] = Empty;
                }
                for (int r = 0; r <= write; r++)
                {
                    _cells[r][c] = _random.NextInt(_config.ColorCount);
                }
            }
        }

        /// <summary>A board without any group of 2+ would be a dead end: regenerate until it is playable.</summary>
        private void EnsurePlayable()
        {
            int guard = 0;
            while (!HasAnyGroup() && guard++ < 100)
            {
                FillAll();
            }
        }

        private bool HasAnyGroup()
        {
            for (int r = 0; r < _config.Rows; r++)
            {
                for (int c = 0; c < _config.Cols; c++)
                {
                    int color = _cells[r][c];
                    if (r + 1 < _config.Rows && _cells[r + 1][c] == color)
                    {
                        return true;
                    }
                    if (c + 1 < _config.Cols && _cells[r][c + 1] == color)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Depth-first flood fill over 4-connected neighbours of the same colour.</summary>
        private List<CellPos> CollectGroup(int row, int col, bool[][] seen)
        {
            var group = new List<CellPos>();
            int color = _cells[row][col];
            if (color == Empty)
            {
                return group;
            }
            var stack = new Stack<CellPos>();
            stack.Push(new CellPos(row, col));
            seen[row][col] = true;
            while (stack.Count > 0)
            {
                CellPos cur = stack.Pop();
                group.Add(cur);
                // Same neighbour order as the Java code (up, down, left, right); it only affects the
                // order of cells inside the group, never the group's membership.
                Visit(cur.Row - 1, cur.Col, color, seen, stack);
                Visit(cur.Row + 1, cur.Col, color, seen, stack);
                Visit(cur.Row, cur.Col - 1, color, seen, stack);
                Visit(cur.Row, cur.Col + 1, color, seen, stack);
            }
            return group;
        }

        private void Visit(int row, int col, int color, bool[][] seen, Stack<CellPos> stack)
        {
            if (InBounds(row, col) && !seen[row][col] && _cells[row][col] == color)
            {
                seen[row][col] = true;
                stack.Push(new CellPos(row, col));
            }
        }

        private bool InBounds(int row, int col)
        {
            return row >= 0 && row < _config.Rows && col >= 0 && col < _config.Cols;
        }

        private bool[][] NewSeenGrid()
        {
            var seen = new bool[_config.Rows][];
            for (int r = 0; r < _config.Rows; r++)
            {
                seen[r] = new bool[_config.Cols];
            }
            return seen;
        }
    }
}
