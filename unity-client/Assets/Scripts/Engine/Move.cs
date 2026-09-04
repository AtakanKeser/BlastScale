namespace BlastScale.Engine
{
    /// <summary>
    /// One recorded player action (port of <c>Move.java</c>). Row/col are ignored for SHUFFLE.
    /// The client records every move it applies and sends the list to the server, which replays it.
    /// </summary>
    public readonly struct Move
    {
        public MoveType Type { get; }
        public int Row { get; }
        public int Col { get; }

        public Move(MoveType type, int row, int col)
        {
            Type = type;
            Row = row;
            Col = col;
        }

        public static Move Tap(int row, int col)
        {
            return new Move(MoveType.TAP, row, col);
        }

        public static Move Hammer(int row, int col)
        {
            return new Move(MoveType.HAMMER, row, col);
        }

        /// <summary>A shuffle has no target cell; zeros are sent so the payload stays valid (@Min(0)).</summary>
        public static Move Shuffle()
        {
            return new Move(MoveType.SHUFFLE, 0, 0);
        }

        public override string ToString()
        {
            return Type + "(" + Row + "," + Col + ")";
        }
    }
}
