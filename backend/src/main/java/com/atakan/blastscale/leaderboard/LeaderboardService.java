package com.atakan.blastscale.leaderboard;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.economy.EconomyService;
import com.atakan.blastscale.economy.Resource;
import com.atakan.blastscale.economy.TransactionReason;
import com.atakan.blastscale.leaderboard.dto.FinalizationResult;
import com.atakan.blastscale.leaderboard.dto.LeaderboardView;
import com.atakan.blastscale.player.PlayerService;
import com.atakan.blastscale.remoteconfig.ConfigKeys;
import com.atakan.blastscale.remoteconfig.RemoteConfigService;
import com.atakan.blastscale.telemetry.TelemetryEventType;
import com.atakan.blastscale.telemetry.TelemetryService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataAccessException;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.ZSetOperations;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;

/**
 * Weekly leaderboard on a Redis sorted set.
 *
 * <pre>
 *   ZINCRBY  lb:weekly:2026-W36  {score}  {playerId}     -- on every level completion, O(log N)
 *   ZREVRANGE lb:weekly:2026-W36 0 99 WITHSCORES         -- top 100
 *   ZREVRANK  lb:weekly:2026-W36 {playerId}              -- my rank
 * </pre>
 *
 * <p>Scores are additive per season. The set lives only in Redis: it is derived data that can
 * be rebuilt from the ledger/telemetry, so losing it is an inconvenience, not a correctness
 * problem. Prizes, on the other hand, are paid through the economy ledger with a per-season
 * reference, which makes finalization safe to re-run.
 */
@Service
public class LeaderboardService {

    private static final Logger log = LoggerFactory.getLogger(LeaderboardService.class);
    private static final Duration KEY_TTL = Duration.ofDays(14);

    private final StringRedisTemplate redis;
    private final PlayerService playerService;
    private final EconomyService economyService;
    private final RemoteConfigService config;
    private final LeaderboardProperties properties;
    private final LeaderboardSeasonRepository seasons;
    private final LeaderboardRewardRepository rewards;
    private final TelemetryService telemetry;
    private final Clock clock;

    public LeaderboardService(StringRedisTemplate redis, PlayerService playerService, EconomyService economyService,
                              RemoteConfigService config, LeaderboardProperties properties,
                              LeaderboardSeasonRepository seasons, LeaderboardRewardRepository rewards,
                              TelemetryService telemetry, Clock clock) {
        this.redis = redis;
        this.playerService = playerService;
        this.economyService = economyService;
        this.config = config;
        this.properties = properties;
        this.seasons = seasons;
        this.rewards = rewards;
        this.telemetry = telemetry;
        this.clock = clock;
    }

    public static String key(String season) {
        return "lb:weekly:" + season;
    }

    public String currentSeason() {
        return LeaderboardSeason.at(Instant.now(clock));
    }

    /** Adds points for the current season. Best effort: a Redis outage never fails a level completion. */
    public void addScore(long playerId, long points) {
        if (points <= 0 || !config.baseConfigBoolean(ConfigKeys.LEADERBOARD_ENABLED)) {
            return;
        }
        String key = key(currentSeason());
        try {
            redis.opsForZSet().incrementScore(key, Long.toString(playerId), points);
            redis.expire(key, KEY_TTL);
        } catch (DataAccessException e) {
            log.warn("Leaderboard update skipped for player {} (Redis unavailable): {}", playerId, e.getMessage());
        }
    }

    public LeaderboardView weekly(long playerId, int limit) {
        String season = currentSeason();
        return view(season, playerId, limit);
    }

    public LeaderboardView view(String season, Long playerId, int limit) {
        String key = key(season);
        try {
            Set<ZSetOperations.TypedTuple<String>> top = redis.opsForZSet().reverseRangeWithScores(key, 0, limit - 1L);
            List<LeaderboardView.Entry> entries = new ArrayList<>();
            if (top != null && !top.isEmpty()) {
                List<Long> ids = top.stream().map(t -> Long.parseLong(t.getValue())).toList();
                Map<Long, String> names = playerService.usernamesOf(ids);
                int rank = 1;
                for (ZSetOperations.TypedTuple<String> tuple : top) {
                    long id = Long.parseLong(tuple.getValue());
                    entries.add(new LeaderboardView.Entry(rank++, id, names.getOrDefault(id, "player" + id), scoreOf(tuple.getScore())));
                }
            }
            Integer myRank = null;
            long myScore = 0;
            if (playerId != null) {
                Long rank = redis.opsForZSet().reverseRank(key, Long.toString(playerId));
                Double score = redis.opsForZSet().score(key, Long.toString(playerId));
                if (rank != null) {
                    myRank = (int) (rank + 1);
                }
                myScore = scoreOf(score);
            }
            boolean finalized = seasons.existsById(season);
            return new LeaderboardView(season, LeaderboardSeason.endOf(season), finalized, entries, myRank, myScore);
        } catch (DataAccessException e) {
            // Degrade explicitly: the client shows "leaderboard temporarily unavailable" and the
            // rest of the game keeps working.
            throw new BlastScaleException(ErrorCode.LEADERBOARD_UNAVAILABLE, "Leaderboard is temporarily unavailable");
        }
    }

    // ------------------------------------------------------------------ finalization

    /**
     * Pays the season prizes exactly once.
     *
     * <p>Three independent guards make re-running this safe (a crashed job, two replicas, an
     * admin clicking twice):
     * <ol>
     *   <li>the caller holds a Redis lock (see {@link LeaderboardFinalizationJob});</li>
     *   <li>a {@code leaderboard_season} row marks a finished finalization;</li>
     *   <li>each prize is a ledger credit with reference {@code leaderboard:{season}} — the
     *       unique key rejects a second payment for the same player even if guards 1 and 2 fail.</li>
     * </ol>
     */
    @Transactional
    public FinalizationResult finalizeSeason(String season, boolean force) {
        if (!LeaderboardSeason.isValid(season)) {
            throw new BlastScaleException(ErrorCode.VALIDATION_ERROR, "Season must look like 2026-W36");
        }
        Optional<LeaderboardSeasonRecord> existing = seasons.findById(season);
        if (existing.isPresent()) {
            List<FinalizationResult.RewardedPlayer> paid = rewards.findByIdSeasonOrderByRankAsc(season).stream()
                    .map(r -> new FinalizationResult.RewardedPlayer(r.getRank(), r.getId().getPlayerId(), r.getScore(), r.getCoins()))
                    .toList();
            return new FinalizationResult(season, true, existing.get().getFinalizedAt(), existing.get().getParticipants(), paid);
        }
        Instant now = Instant.now(clock);
        if (!force && now.isBefore(LeaderboardSeason.endOf(season))) {
            throw new BlastScaleException(ErrorCode.LEADERBOARD_SEASON_ACTIVE, "Season " + season + " has not ended yet");
        }

        String key = key(season);
        int prizeCount = properties.rewardCoins().size();
        Set<ZSetOperations.TypedTuple<String>> top = redis.opsForZSet().reverseRangeWithScores(key, 0, prizeCount - 1L);
        Long participants = redis.opsForZSet().zCard(key);

        List<FinalizationResult.RewardedPlayer> paid = new ArrayList<>();
        int rank = 1;
        if (top != null) {
            for (ZSetOperations.TypedTuple<String> tuple : top) {
                long playerId = Long.parseLong(tuple.getValue());
                int coins = properties.rewardCoins().getOrDefault(rank, 0);
                String reference = "leaderboard:" + season;
                if (coins > 0 && !economyService.wasApplied(playerId, TransactionReason.LEADERBOARD_REWARD, reference)) {
                    economyService.credit(playerId, Resource.COIN, coins, TransactionReason.LEADERBOARD_REWARD, reference);
                    telemetry.record(TelemetryEventType.LEADERBOARD_REWARD_GRANTED, playerId, "leaderboard", season,
                            Map.of("rank", rank, "coins", coins, "score", scoreOf(tuple.getScore())));
                }
                rewards.save(new LeaderboardReward(season, playerId, rank, scoreOf(tuple.getScore()), coins));
                paid.add(new FinalizationResult.RewardedPlayer(rank, playerId, scoreOf(tuple.getScore()), coins));
                rank++;
            }
        }
        int participantCount = participants == null ? 0 : participants.intValue();
        seasons.save(new LeaderboardSeasonRecord(season, now, participantCount, paid.size()));
        telemetry.record(TelemetryEventType.LEADERBOARD_FINALIZED, null, "leaderboard", season,
                Map.of("participants", participantCount, "rewarded", paid.size()));
        log.info("Finalized leaderboard season {}: {} participants, {} rewarded", season, participantCount, paid.size());
        return new FinalizationResult(season, false, now, participantCount, paid);
    }

    private static long scoreOf(Double score) {
        return score == null ? 0 : Math.round(score);
    }
}
