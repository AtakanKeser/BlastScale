namespace BlastScale.Engine
{
    /// <summary>
    /// A board coordinate. The Java engine uses <c>int[]{row, col}</c> pairs; a tiny struct is the
    /// idiomatic C# equivalent and keeps group lists allocation-light.
    /// </summary>
    public readonly struct CellPos
    {
        public int Row { get; }
        public int Col { get; }

        public CellPos(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public override string ToString()
        {
            return "(" + Row + "," + Col + ")";
        }
    }
}
