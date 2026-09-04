package com.atakan.blastscale.progression;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.common.metrics.GameplayMetrics;
import com.atakan.blastscale.economy.EconomyService;
import com.atakan.blastscale.economy.Resource;
import com.atakan.blastscale.economy.ResourceChange;
import com.atakan.blastscale.economy.TransactionReason;
import com.atakan.blastscale.economy.WalletSnapshot;
import com.atakan.blastscale.economy.reward.LevelResult;
import com.atakan.blastscale.economy.reward.Reward;
import com.atakan.blastscale.economy.reward.RewardContext;
import com.atakan.blastscale.economy.reward.RewardService;
import com.atakan.blastscale.event.LiveEventService;
import com.atakan.blastscale.event.dto.EventPointsAwarded;
import com.atakan.blastscale.leaderboard.LeaderboardService;
import com.atakan.blastscale.level.LevelDefinition;
import com.atakan.blastscale.level.LevelDefinitionService;
import com.atakan.blastscale.level.engine.BoardEngine;
import com.atakan.blastscale.level.engine.SimulationResult;
import com.atakan.blastscale.player.Player;
import com.atakan.blastscale.player.PlayerService;
import com.atakan.blastscale.progression.dto.LevelCompleteRequest;
import com.atakan.blastscale.progression.dto.LevelCompleteResponse;
import com.atakan.blastscale.progression.dto.LevelFailRequest;
import com.atakan.blastscale.progression.dto.LevelFailResponse;
import com.atakan.blastscale.progression.dto.LevelStartResponse;
import com.atakan.blastscale.progression.dto.ProgressView;
import com.atakan.blastscale.progression.dto.SessionView;
import com.atakan.blastscale.progression.validation.CompletionValidationChain;
import com.atakan.blastscale.progression.validation.LevelCompletionContext;
import com.atakan.blastscale.remoteconfig.RemoteConfigService;
import com.atakan.blastscale.remoteconfig.ResolvedConfig;
import com.atakan.blastscale.telemetry.TelemetryEventType;
import com.atakan.blastscale.telemetry.TelemetryService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.domain.PageRequest;
import org.springframework.stereotype.Service;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.transaction.support.TransactionSynchronization;
import org.springframework.transaction.support.TransactionSynchronizationManager;
import org.springframework.transaction.support.TransactionTemplate;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ThreadLocalRandom;

/**
 * The core gameplay loop, orchestrating every other module:
 *
 * <pre>
 *   POST /levels/{id}/start                    POST /levels/{id}/complete
 *   ------------------------                   ---------------------------
 *   check level unlocked                       load session (COMPLETED? -> replay stored result)
 *   abandon other active sessions              run CompletionValidationChain (session, progression,
 *   consume a life (ledger)                        duration, score bounds, server-side replay)
 *   create session with server seed            ---- write transaction ----
 *   emit LEVEL_STARTED                         claim session: UPDATE ... WHERE status = ACTIVE
 *                                              update level progress (stars, best score, first clear)
 *                                              calculate reward (Strategy: experiment / event / standard)
 *                                              apply reward + booster debits (ledger, wallet lock)
 *                                              advance player level, award live-event points
 *                                              emit LEVEL_COMPLETED (outbox, same transaction)
 *                                              ---- after commit ----
 *                                              add score to the Redis weekly leaderboard
 * </pre>
 *
 * <p>Exactly-once reward, three layers deep: the Idempotency-Key guard in Redis (fast path for
 * retried requests), the conditional session UPDATE (only one request can move a session out of
 * ACTIVE), and the ledger's unique (player, reason, reference) key (the session id is the
 * reference). Any one of them alone would be enough; together they cover every failure mode.
 */
@Service
public class ProgressionService {

    private static final Logger log = LoggerFactory.getLogger(ProgressionService.class);

    private final PlayerService playerService;
    private final LevelDefinitionService levels;
    private final EconomyService economyService;
    private final RewardService rewardService;
    private final RemoteConfigService config;
    private final LiveEventService eventService;
    private final LeaderboardService leaderboardService;
    private final GameSessionRepository sessions;
    private final LevelProgressRepository progressRepository;
    private final CompletionValidationChain validationChain;
    private final TelemetryService telemetry;
    private final GameplayMetrics metrics;
    private final GameplayProperties properties;
    private final TransactionTemplate transaction;
    private final Clock clock;

    public ProgressionService(PlayerService playerService, LevelDefinitionService levels, EconomyService economyService,
                              RewardService rewardService, RemoteConfigService config, LiveEventService eventService,
                              LeaderboardService leaderboardService, GameSessionRepository sessions,
                              LevelProgressRepository progressRepository, CompletionValidationChain validationChain,
                              TelemetryService telemetry, GameplayMetrics metrics, GameplayProperties properties,
                              PlatformTransactionManager transactionManager, Clock clock) {
        this.playerService = playerService;
        this.levels = levels;
        this.economyService = economyService;
        this.rewardService = rewardService;
        this.config = config;
        this.eventService = eventService;
        this.leaderboardService = leaderboardService;
        this.sessions = sessions;
        this.progressRepository = progressRepository;
        this.validationChain = validationChain;
        this.telemetry = telemetry;
        this.metrics = metrics;
        this.properties = properties;
        this.transaction = new TransactionTemplate(transactionManager);
        this.clock = clock;
    }

    // ------------------------------------------------------------------ start

    @Transactional
    public LevelStartResponse startLevel(long playerId, int levelId) {
        // A player plays one level at a time; anything still open is abandoned (the life stays spent).
        sessions.abandonActive(playerId);

        Player player = playerService.requirePlayer(playerId);
        if (levelId > player.getCurrentLevel()) {
            throw new BlastScaleException(ErrorCode.LEVEL_LOCKED,
                    "Level " + levelId + " is locked; current level is " + player.getCurrentLevel(),
                    Map.of("currentLevel", player.getCurrentLevel()));
        }
        LevelDefinition level = levels.get(levelId);
        Instant now = Instant.now(clock);
        String sessionId = UUID.randomUUID().toString();
        // The server picks the seed: the client cannot fish for an easy board.
        int seed = ThreadLocalRandom.current().nextInt(1, Integer.MAX_VALUE);

        WalletSnapshot wallet = economyService.consumeLife(playerId, sessionId); // throws NO_LIVES_LEFT
        sessions.save(new GameSession(sessionId, playerId, levelId, seed, level.getVersion(), now));

        LevelProgress progress = progressRepository.findById(new LevelProgress.Id(playerId, levelId))
                .orElseGet(() -> new LevelProgress(playerId, levelId, now));
        progress.recordAttempt(now);
        progressRepository.save(progress);
        player.touch(now);

        telemetry.record(TelemetryEventType.LEVEL_STARTED, playerId, "session", sessionId,
                Map.of("level", levelId, "seed", seed, "livesRemaining", wallet.lives(), "attempt", progress.getAttempts()));
        metrics.levelStarted();
        return new LevelStartResponse(sessionId, levelId, seed, level.toBoardConfig(), level.getVersion(),
                wallet.lives(), now, now.plus(properties.sessionTtl()));
    }

    // ------------------------------------------------------------------ complete

    /**
     * Not annotated {@code @Transactional} on purpose: validation (including the board replay) runs
     * without holding any database lock; only the short write phase is transactional.
     */
    public LevelCompleteResponse completeLevel(long playerId, int levelId, LevelCompleteRequest request) {
        long startedNanos = System.nanoTime();
        Instant now = Instant.now(clock);

        GameSession session = sessions.findByIdAndPlayerId(request.sessionId(), playerId)
                .orElseThrow(() -> new BlastScaleException(ErrorCode.SESSION_NOT_FOUND, "Session not found"));
        if (session.getStatus() == SessionStatus.COMPLETED) {
            // Retry without an Idempotency-Key (or a replay attempt): answer with the stored result.
            metrics.levelCompletion("replayed");
            return alreadyProcessed(session);
        }

        Player player = playerService.requirePlayer(playerId);
        LevelDefinition level = levels.get(levelId);
        WalletSnapshot wallet = economyService.getWallet(playerId);
        LevelCompletionContext context = new LevelCompletionContext(player, session, level, wallet, request.score(),
                request.movesUsed(), request.moves(), request.extraMovesUsed(), now);

        CompletionValidationChain.Rejection rejection = validationChain.validate(context);
        if (rejection != null) {
            metrics.levelCompletion("rejected");
            telemetry.record(TelemetryEventType.COMPLETION_REJECTED, playerId, "session", session.getId(), Map.of(
                    "level", levelId, "validator", rejection.validator(), "code", rejection.result().code().name(),
                    "claimedScore", request.score(), "moves", request.moves().size()));
            log.info("Rejected completion of session {} by validator '{}': {}", session.getId(),
                    rejection.validator(), rejection.result().message());
            throw new BlastScaleException(rejection.result().code(), rejection.result().message(), rejection.result().details());
        }

        SimulationResult simulation = context.simulation();
        LevelCompleteResponse response = transaction.execute(status ->
                commitCompletion(playerId, level, session, simulation, request, now));
        metrics.rewardProcessing(Duration.ofNanos(System.nanoTime() - startedNanos));
        return response;
    }

    private LevelCompleteResponse commitCompletion(long playerId, LevelDefinition level, GameSession session,
                                                   SimulationResult simulation, LevelCompleteRequest request, Instant now) {
        int levelId = level.getLevelNumber();
        int claimed = sessions.closeIfActive(session.getId(), SessionStatus.COMPLETED, now,
                simulation.score(), simulation.movesUsed(), simulation.stars());
        if (claimed == 0) {
            // Lost the race against a concurrent request for the same session.
            GameSession current = sessions.findById(session.getId()).orElseThrow();
            if (current.getStatus() == SessionStatus.COMPLETED) {
                metrics.levelCompletion("replayed");
                return alreadyProcessed(current);
            }
            throw new BlastScaleException(ErrorCode.SESSION_NOT_ACTIVE, "Session is " + current.getStatus());
        }
        GameSession closed = sessions.findById(session.getId()).orElseThrow();

        LevelProgress progress = progressRepository.findById(new LevelProgress.Id(playerId, levelId))
                .orElseGet(() -> new LevelProgress(playerId, levelId, now));
        boolean firstClear = !progress.isCleared();
        boolean newBest = simulation.score() > progress.getBestScore();
        int starsGained = progress.recordClear(simulation.score(), simulation.stars(), now);
        progressRepository.save(progress);

        ResolvedConfig cfg = config.resolveFor(playerId);
        LevelResult result = new LevelResult(levelId, simulation.score(), simulation.stars(), simulation.movesUsed(),
                level.getMoveLimit(), firstClear);
        Reward reward = rewardService.calculate(new RewardContext(playerId, result, cfg, eventService.doubleRewardMultiplier()));

        List<ResourceChange> changes = new ArrayList<>();
        changes.add(ResourceChange.credit(Resource.COIN, reward.coins()));
        if (starsGained > 0) {
            changes.add(ResourceChange.credit(Resource.STAR, starsGained));
        }
        changes.addAll(boosterDebits(simulation, request.extraMovesUsed()));
        WalletSnapshot wallet = economyService.apply(playerId, changes, TransactionReason.LEVEL_COMPLETE, session.getId());

        int nextLevel = playerService.advanceLevel(playerId, levelId);
        List<EventPointsAwarded> eventPoints = eventService.recordLevelCompletion(playerId, nextLevel, levelId);
        closed.recordReward(reward.coins(), reward.strategy(), reward.multiplier());

        telemetry.record(TelemetryEventType.LEVEL_COMPLETED, playerId, "session", session.getId(), Map.of(
                "level", levelId, "score", simulation.score(), "stars", simulation.stars(),
                "moves", simulation.movesUsed(), "durationSeconds", Duration.between(session.getStartedAt(), now).getSeconds(),
                "rewardCoins", reward.coins(), "rewardStrategy", reward.strategy(), "firstClear", firstClear));

        // Redis work only after the database commit: a rolled back completion must not leave a
        // phantom score on the leaderboard.
        int score = simulation.score();
        TransactionSynchronizationManager.registerSynchronization(new TransactionSynchronization() {
            @Override
            public void afterCommit() {
                leaderboardService.addScore(playerId, score);
                metrics.levelCompletion("success");
            }
        });
        return new LevelCompleteResponse(LevelCompleteResponse.COMPLETED, session.getId(), levelId, simulation.score(),
                simulation.stars(), firstClear, newBest, reward, wallet, nextLevel, eventPoints);
    }

    private LevelCompleteResponse alreadyProcessed(GameSession session) {
        long playerId = session.getPlayerId();
        Reward reward = new Reward(session.getRewardCoins() == null ? 0 : session.getRewardCoins(),
                session.getStars() == null ? 0 : session.getStars(),
                session.getRewardMultiplier() == null ? 1.0 : session.getRewardMultiplier(),
                session.getRewardStrategy() == null ? "UNKNOWN" : session.getRewardStrategy());
        return new LevelCompleteResponse(LevelCompleteResponse.ALREADY_PROCESSED, session.getId(), session.getLevelId(),
                session.getScore() == null ? 0 : session.getScore(), reward.stars(), false, false, reward,
                economyService.getWallet(playerId), playerService.requirePlayer(playerId).getCurrentLevel(), List.of());
    }

    // ------------------------------------------------------------------ fail

    @Transactional
    public LevelFailResponse failLevel(long playerId, int levelId, LevelFailRequest request) {
        GameSession session = sessions.findByIdAndPlayerId(request.sessionId(), playerId)
                .orElseThrow(() -> new BlastScaleException(ErrorCode.SESSION_NOT_FOUND, "Session not found"));
        if (session.getLevelId() != levelId) {
            throw new BlastScaleException(ErrorCode.SESSION_LEVEL_MISMATCH, "Session belongs to level " + session.getLevelId());
        }
        LevelDefinition level = levels.get(levelId);
        Instant now = Instant.now(clock);
        // Replay leniently: we only need the booster usage; a broken move list just yields fewer boosters.
        SimulationResult simulation = BoardEngine.simulate(level.toBoardConfig(), session.getSeed(), request.moves(), request.extraMovesUsed());

        int claimed = sessions.closeIfActive(session.getId(), SessionStatus.FAILED, now, simulation.score(), simulation.movesUsed(), 0);
        if (claimed == 0) {
            GameSession current = sessions.findById(session.getId()).orElseThrow();
            if (current.getStatus() == SessionStatus.FAILED) {
                return new LevelFailResponse(LevelCompleteResponse.ALREADY_PROCESSED, session.getId(), levelId,
                        current.getScore() == null ? 0 : current.getScore(), economyService.getWallet(playerId));
            }
            throw new BlastScaleException(ErrorCode.SESSION_NOT_ACTIVE, "Session is " + current.getStatus());
        }
        WalletSnapshot wallet;
        List<ResourceChange> debits = boosterDebits(simulation, request.extraMovesUsed());
        if (debits.isEmpty()) {
            wallet = economyService.getWallet(playerId);
        } else {
            wallet = economyService.apply(playerId, debits, TransactionReason.USE_BOOSTER, session.getId());
        }
        telemetry.record(TelemetryEventType.LEVEL_FAILED, playerId, "session", session.getId(), Map.of(
                "level", levelId, "score", simulation.score(), "moves", simulation.movesUsed(),
                "durationSeconds", Duration.between(session.getStartedAt(), now).getSeconds()));
        metrics.levelCompletion("failed");
        return new LevelFailResponse("FAILED", session.getId(), levelId, simulation.score(), wallet);
    }

    private static List<ResourceChange> boosterDebits(SimulationResult simulation, boolean extraMovesUsed) {
        List<ResourceChange> debits = new ArrayList<>();
        if (simulation.hammersUsed() > 0) {
            debits.add(ResourceChange.debit(Resource.BOOSTER_HAMMER, simulation.hammersUsed()));
        }
        if (simulation.shufflesUsed() > 0) {
            debits.add(ResourceChange.debit(Resource.BOOSTER_SHUFFLE, simulation.shufflesUsed()));
        }
        if (extraMovesUsed) {
            debits.add(ResourceChange.debit(Resource.BOOSTER_EXTRA_MOVES, 1));
        }
        return debits;
    }

    // ------------------------------------------------------------------ reads

    @Transactional(readOnly = true)
    public ProgressView progress(long playerId) {
        Player player = playerService.requirePlayer(playerId);
        List<LevelProgress> rows = progressRepository.findByIdPlayerIdOrderByIdLevelIdAsc(playerId);
        List<ProgressView.LevelEntry> entries = rows.stream()
                .map(p -> new ProgressView.LevelEntry(p.getId().getLevelId(), p.getStars(), p.getBestScore(),
                        p.getAttempts(), p.isCleared(), p.getCompletedAt()))
                .toList();
        int totalStars = rows.stream().mapToInt(LevelProgress::getStars).sum();
        return new ProgressView(player.getCurrentLevel(), totalStars, entries);
    }

    @Transactional(readOnly = true)
    public List<SessionView> recentSessions(long playerId, int limit) {
        return sessions.findByPlayerIdOrderByStartedAtDesc(playerId, PageRequest.of(0, limit)).stream()
                .map(SessionView::from).toList();
    }

    /** Housekeeping hook for the scheduler. */
    @Transactional
    public int expireStaleSessions() {
        return sessions.expireOlderThan(Instant.now(clock).minus(properties.sessionTtl()));
    }
}
