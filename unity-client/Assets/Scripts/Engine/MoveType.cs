namespace BlastScale.Engine
{
    /// <summary>
    /// Kinds of player actions the engine understands (port of <c>MoveType.java</c>).
    /// The members are upper-case on purpose: <c>ToString()</c> must yield the exact strings the
    /// server expects on the wire ("TAP", "HAMMER", "SHUFFLE").
    /// <list type="bullet">
    ///   <item><c>TAP</c>: pop a group of 2+ same-coloured blocks — counts as a move</item>
    ///   <item><c>HAMMER</c>: remove one block of any colour — consumes a HAMMER booster, no move</item>
    ///   <item><c>SHUFFLE</c>: regenerate the whole board — consumes a SHUFFLE booster, no move</item>
    /// </list>
    /// </summary>
    public enum MoveType
    {
        TAP,
        HAMMER,
        SHUFFLE
    }
}
