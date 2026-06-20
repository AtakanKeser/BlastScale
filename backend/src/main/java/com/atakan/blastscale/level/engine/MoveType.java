package com.atakan.blastscale.level.engine;

/**
 * Kinds of player actions the engine understands.
 * <ul>
 *   <li>{@code TAP}: pop a group of 2+ same-coloured blocks — counts as a move</li>
 *   <li>{@code HAMMER}: remove one block of any colour — consumes a HAMMER booster, no move</li>
 *   <li>{@code SHUFFLE}: regenerate the whole board — consumes a SHUFFLE booster, no move</li>
 * </ul>
 */
public enum MoveType {
    TAP,
    HAMMER,
    SHUFFLE
}
