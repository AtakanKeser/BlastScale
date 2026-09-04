package com.atakan.blastscale.progression.validation;

import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.common.metrics.GameplayMetrics;
import com.atakan.blastscale.economy.WalletSnapshot;
import com.atakan.blastscale.level.LevelDefinition;
import com.atakan.blastscale.level.engine.BoardEngine;
import com.atakan.blastscale.level.engine.GreedySolver;
import com.atakan.blastscale.level.engine.Move;
import com.atakan.blastscale.level.engine.MoveType;
import com.atakan.blastscale.level.engine.SimulationResult;
import com.atakan.blastscale.player.Player;
import com.atakan.blastscale.player.PlayerRole;
import com.atakan.blastscale.progression.GameSession;
import com.atakan.blastscale.progression.GameplayProperties;
import io.micrometer.core.instrument.simple.SimpleMeterRegistry;
import org.junit.jupiter.api.Test;
import org.springframework.test.util.ReflectionTestUtils;

import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

import static org.assertj.core.api.Assertions.assertThat;

/** Each link of the anti-cheat chain in isolation, plus the chain's short-circuit behaviour. */
class CompletionValidatorsTest {

    private static final Instant NOW = Instant.parse("2026-09-04T12:00:00Z");
    private static final int SEED = 4242;
    private static final GameplayProperties PROPS = new GameplayProperties(Duration.ofHours(2), 150);

    private final LevelDefinition level = new LevelDefinition(3, 1, 8, 8, 4, 20, 1790, List.of(1790, 2238, 2685), Map.of(), "test", NOW);
    private final SimpleMeterRegistry registry = new SimpleMeterRegistry();
    private final GameplayMetrics metrics = new GameplayMetrics(registry);

    private Player player(long id, int currentLevel) {
        Player player = new Player("p" + id, null, "device" + id, PlayerRole.PLAYER, NOW);
        ReflectionTestUtils.setField(player, "id", id);
        player.setCurrentLevel(currentLevel);
        return player;
    }

    private GameSession session(long playerId, Instant startedAt) {
        return new GameSession("session-1", playerId, 3, SEED, 1, startedAt);
    }

    private static WalletSnapshot wallet(int hammers, int shuffles, int extraMoves) {
        return new WalletSnapshot(100, 3, 5, 0, 0, Map.of("HAMMER", hammers, "SHUFFLE", shuffles, "EXTRA_MOVES", extraMoves));
    }

    private LevelCompletionContext context(Player player, GameSession session, List<Move> moves, int claimedScore,
                                           WalletSnapshot wallet, boolean extraMoves) {
        return new LevelCompletionContext(player, session, level, wallet, claimedScore, (int) moves.stream().filter(m -> m.type() == MoveType.TAP).count(),
                moves, extraMoves, NOW);
    }

    private List<Move> winningMoves() {
        return GreedySolver.solve(level.toBoardConfig(), SEED);
    }

    private int scoreOf(List<Move> moves) {
        return BoardEngine.simulate(level.toBoardConfig(), SEED, moves, false).score();
    }

    // ------------------------------------------------------------------ session

    @Test
    void sessionValidatorRejectsForeignSession() {
        ValidationResult r = new SessionValidator(PROPS).validate(context(player(2, 5), session(1, NOW.minusSeconds(30)), winningMoves(), 0, wallet(0, 0, 0), false));
        assertThat(r.valid()).isFalse();
        assertThat(r.code()).isEqualTo(ErrorCode.SESSION_NOT_FOUND);
    }

    @Test
    void sessionValidatorRejectsExpiredSession() {
        ValidationResult r = new SessionValidator(PROPS).validate(context(player(1, 5), session(1, NOW.minus(Duration.ofHours(3))), winningMoves(), 0, wallet(0, 0, 0), false));
        assertThat(r.code()).isEqualTo(ErrorCode.SESSION_EXPIRED);
    }

    @Test
    void sessionValidatorRejectsLevelMismatch() {
        GameSession other = new GameSession("s2", 1L, 4, SEED, 1, NOW.minusSeconds(30));
        ValidationResult r = new SessionValidator(PROPS).validate(context(player(1, 5), other, winningMoves(), 0, wallet(0, 0, 0), false));
        assertThat(r.code()).isEqualTo(ErrorCode.SESSION_LEVEL_MISMATCH);
    }

    // ------------------------------------------------------------------ progression

    @Test
    void progressionValidatorRejectsLockedLevel() {
        ValidationResult r = new ProgressionValidator().validate(context(player(1, 2), session(1, NOW), winningMoves(), 0, wallet(0, 0, 0), false));
        assertThat(r.code()).isEqualTo(ErrorCode.LEVEL_LOCKED);
        assertThat(new ProgressionValidator().validate(context(player(1, 3), session(1, NOW), winningMoves(), 0, wallet(0, 0, 0), false)).valid()).isTrue();
    }

    // ------------------------------------------------------------------ duration

    @Test
    void durationValidatorRejectsSuperhumanSpeed() {
        List<Move> moves = winningMoves();
        GameSession justStarted = session(1, NOW.minusMillis(100));
        ValidationResult r = new DurationValidator(PROPS).validate(context(player(1, 5), justStarted, moves, 0, wallet(0, 0, 0), false));
        assertThat(r.code()).isEqualTo(ErrorCode.SUSPICIOUS_DURATION);
        GameSession plausible = session(1, NOW.minusSeconds(60));
        assertThat(new DurationValidator(PROPS).validate(context(player(1, 5), plausible, moves, 0, wallet(0, 0, 0), false)).valid()).isTrue();
    }

    // ------------------------------------------------------------------ score bounds

    @Test
    void scoreValidatorRejectsTooManyMovesImpossibleScoresAndUnownedBoosters() {
        List<Move> tooMany = new ArrayList<>();
        for (int i = 0; i < 21; i++) {
            tooMany.add(Move.tap(0, 0));
        }
        assertThat(new ScoreValidator().validate(context(player(1, 5), session(1, NOW), tooMany, 0, wallet(0, 0, 0), false)).code())
                .isEqualTo(ErrorCode.TOO_MANY_MOVES);

        List<Move> moves = winningMoves();
        assertThat(new ScoreValidator().validate(context(player(1, 5), session(1, NOW), moves, 99_999_999, wallet(0, 0, 0), false)).code())
                .isEqualTo(ErrorCode.SCORE_OUT_OF_RANGE);

        List<Move> withHammer = new ArrayList<>(moves);
        withHammer.add(0, new Move(MoveType.HAMMER, 1, 1));
        assertThat(new ScoreValidator().validate(context(player(1, 5), session(1, NOW), withHammer, 0, wallet(0, 0, 0), false)).code())
                .isEqualTo(ErrorCode.INSUFFICIENT_BOOSTERS);
        assertThat(new ScoreValidator().validate(context(player(1, 5), session(1, NOW), withHammer, 0, wallet(1, 0, 0), false)).valid()).isTrue();
        assertThat(new ScoreValidator().validate(context(player(1, 5), session(1, NOW), moves, 0, wallet(0, 0, 0), true)).code())
                .isEqualTo(ErrorCode.INSUFFICIENT_BOOSTERS);
    }

    // ------------------------------------------------------------------ replay

    @Test
    void replayValidatorAcceptsHonestResultAndStoresSimulation() {
        List<Move> moves = winningMoves();
        LevelCompletionContext ctx = context(player(1, 5), session(1, NOW), moves, scoreOf(moves), wallet(0, 0, 0), false);
        assertThat(new ReplayValidator().validate(ctx).valid()).isTrue();
        SimulationResult sim = ctx.simulation();
        assertThat(sim.objectiveReached()).isTrue();
        assertThat(sim.stars()).isBetween(1, 3);
    }

    @Test
    void replayValidatorRejectsInflatedScore() {
        List<Move> moves = winningMoves();
        ValidationResult r = new ReplayValidator().validate(context(player(1, 5), session(1, NOW), moves, scoreOf(moves) + 10, wallet(0, 0, 0), false));
        assertThat(r.code()).isEqualTo(ErrorCode.SCORE_MISMATCH);
        assertThat(r.details()).containsKeys("claimedScore", "serverScore");
    }

    @Test
    void replayValidatorRejectsUnfinishedLevelAndIllegalMoves() {
        List<Move> partial = winningMoves().subList(0, 1);
        ValidationResult r = new ReplayValidator().validate(context(player(1, 5), session(1, NOW), partial, scoreOf(partial), wallet(0, 0, 0), false));
        assertThat(r.code()).isEqualTo(ErrorCode.OBJECTIVE_NOT_REACHED);

        List<Move> illegal = List.of(Move.tap(9, 9));
        assertThat(new ReplayValidator().validate(context(player(1, 5), session(1, NOW), illegal, 0, wallet(0, 0, 0), false)).code())
                .isEqualTo(ErrorCode.INVALID_MOVE_SEQUENCE);
    }

    // ------------------------------------------------------------------ chain

    @Test
    void chainStopsAtFirstFailureAndCountsIt() {
        CompletionValidationChain chain = new CompletionValidationChain(List.of(
                new SessionValidator(PROPS), new ProgressionValidator(), new DurationValidator(PROPS),
                new ScoreValidator(), new ReplayValidator()), metrics);
        assertThat(chain.validatorNames()).containsExactly("session", "progression", "duration", "score", "replay");

        // locked level AND inflated score: the progression validator fires first, replay never runs
        LevelCompletionContext ctx = context(player(1, 1), session(1, NOW.minusSeconds(60)), winningMoves(), 999_999, wallet(0, 0, 0), false);
        CompletionValidationChain.Rejection rejection = chain.validate(ctx);
        assertThat(rejection.validator()).isEqualTo("progression");
        assertThat(ctx.simulation()).isNull();
        assertThat(registry.get("blastscale_completion_rejected_total").tag("validator", "progression").counter().count()).isEqualTo(1.0);

        List<Move> moves = winningMoves();
        LevelCompletionContext honest = context(player(1, 3), session(1, NOW.minusSeconds(60)), moves, scoreOf(moves), wallet(0, 0, 0), false);
        assertThat(chain.validate(honest)).isNull();
        assertThat(honest.simulation()).isNotNull();
    }
}
