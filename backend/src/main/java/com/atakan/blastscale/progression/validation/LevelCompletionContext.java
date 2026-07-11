package com.atakan.blastscale.progression.validation;

import com.atakan.blastscale.economy.WalletSnapshot;
import com.atakan.blastscale.level.LevelDefinition;
import com.atakan.blastscale.level.engine.Move;
import com.atakan.blastscale.level.engine.MoveType;
import com.atakan.blastscale.level.engine.SimulationResult;
import com.atakan.blastscale.player.Player;
import com.atakan.blastscale.progression.GameSession;

import java.time.Instant;
import java.util.List;

/**
 * Everything the validators look at for one completion request. Inputs are immutable; the
 * {@link ReplayValidator} stores its simulation here so the orchestrator can use the server-side
 * result without replaying twice.
 */
public final class LevelCompletionContext {

    private final Player player;
    private final GameSession session;
    private final LevelDefinition level;
    private final WalletSnapshot wallet;
    private final int claimedScore;
    private final int claimedMoves;
    private final List<Move> moves;
    private final boolean extraMovesUsed;
    private final Instant now;
    private SimulationResult simulation;

    public LevelCompletionContext(Player player, GameSession session, LevelDefinition level, WalletSnapshot wallet,
                                  int claimedScore, int claimedMoves, List<Move> moves, boolean extraMovesUsed, Instant now) {
        this.player = player;
        this.session = session;
        this.level = level;
        this.wallet = wallet;
        this.claimedScore = claimedScore;
        this.claimedMoves = claimedMoves;
        this.moves = moves;
        this.extraMovesUsed = extraMovesUsed;
        this.now = now;
    }

    public Player player() {
        return player;
    }

    public GameSession session() {
        return session;
    }

    public LevelDefinition level() {
        return level;
    }

    public WalletSnapshot wallet() {
        return wallet;
    }

    public int claimedScore() {
        return claimedScore;
    }

    public int claimedMoves() {
        return claimedMoves;
    }

    public List<Move> moves() {
        return moves;
    }

    public boolean extraMovesUsed() {
        return extraMovesUsed;
    }

    public Instant now() {
        return now;
    }

    public SimulationResult simulation() {
        return simulation;
    }

    public void setSimulation(SimulationResult simulation) {
        this.simulation = simulation;
    }

    public long countMoves(MoveType type) {
        return moves.stream().filter(m -> m.type() == type).count();
    }
}
