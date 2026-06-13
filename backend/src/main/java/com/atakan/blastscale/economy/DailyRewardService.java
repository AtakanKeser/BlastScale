package com.atakan.blastscale.economy;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.economy.dto.DailyRewardResult;
import com.atakan.blastscale.economy.dto.DailyRewardStatus;
import com.atakan.blastscale.remoteconfig.ConfigKeys;
import com.atakan.blastscale.remoteconfig.RemoteConfigService;
import com.atakan.blastscale.remoteconfig.ResolvedConfig;
import com.atakan.blastscale.telemetry.TelemetryEventType;
import com.atakan.blastscale.telemetry.TelemetryService;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Clock;
import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneOffset;
import java.util.Map;
import java.util.Optional;

/**
 * Daily login reward with a streak bonus. The amount comes from remote config (and can therefore
 * be changed, or A/B tested, without a release). A UTC calendar day is the claim window.
 */
@Service
public class DailyRewardService {

    private static final int MAX_STREAK_BONUS_DAYS = 6;

    private final DailyRewardClaimRepository claims;
    private final EconomyService economyService;
    private final RemoteConfigService config;
    private final TelemetryService telemetry;
    private final Clock clock;

    public DailyRewardService(DailyRewardClaimRepository claims, EconomyService economyService,
                              RemoteConfigService config, TelemetryService telemetry, Clock clock) {
        this.claims = claims;
        this.economyService = economyService;
        this.config = config;
        this.telemetry = telemetry;
        this.clock = clock;
    }

    @Transactional(readOnly = true)
    public DailyRewardStatus status(long playerId) {
        LocalDate today = LocalDate.now(clock.withZone(ZoneOffset.UTC));
        Optional<DailyRewardClaim> last = claims.findTopByIdPlayerIdOrderByIdClaimedOnDesc(playerId);
        boolean claimedToday = last.map(c -> c.getId().getClaimedOn().equals(today)).orElse(false);
        int nextStreak = nextStreak(last, today);
        ResolvedConfig cfg = config.resolveFor(playerId);
        Instant nextClaimAt = today.plusDays(1).atStartOfDay(ZoneOffset.UTC).toInstant();
        return new DailyRewardStatus(!claimedToday, last.map(DailyRewardClaim::getStreak).orElse(0),
                coinsFor(cfg, claimedToday ? nextStreak + 1 : nextStreak), claimedToday ? nextClaimAt : Instant.now(clock));
    }

    @Transactional
    public DailyRewardResult claim(long playerId) {
        LocalDate today = LocalDate.now(clock.withZone(ZoneOffset.UTC));
        Optional<DailyRewardClaim> last = claims.findTopByIdPlayerIdOrderByIdClaimedOnDesc(playerId);
        Instant nextClaimAt = today.plusDays(1).atStartOfDay(ZoneOffset.UTC).toInstant();
        if (last.isPresent() && last.get().getId().getClaimedOn().equals(today)) {
            throw new BlastScaleException(ErrorCode.DAILY_REWARD_ALREADY_CLAIMED, "Daily reward already claimed today",
                    Map.of("nextClaimAt", nextClaimAt.toString()));
        }
        int streak = nextStreak(last, today);
        ResolvedConfig cfg = config.resolveFor(playerId);
        int coins = coinsFor(cfg, streak);

        // The claim row's primary key (player, day) is the exactly-once guard; the ledger's
        // (reason, reference=day) uniqueness is the second one.
        claims.save(new DailyRewardClaim(playerId, today, streak, coins, Instant.now(clock)));
        WalletSnapshot wallet = economyService.credit(playerId, Resource.COIN, coins,
                TransactionReason.DAILY_REWARD, today.toString());
        telemetry.record(TelemetryEventType.DAILY_REWARD_CLAIMED, playerId, "daily-reward", today.toString(),
                Map.of("coins", coins, "streak", streak));
        return new DailyRewardResult(coins, streak, nextClaimAt, wallet);
    }

    private static int nextStreak(Optional<DailyRewardClaim> last, LocalDate today) {
        return last.filter(c -> c.getId().getClaimedOn().equals(today.minusDays(1)))
                .map(c -> c.getStreak() + 1)
                .orElse(1);
    }

    private static int coinsFor(ResolvedConfig cfg, int streak) {
        int base = cfg.getInt(ConfigKeys.DAILY_REWARD_COINS);
        int bonus = cfg.getInt(ConfigKeys.DAILY_REWARD_STREAK_BONUS);
        return base + bonus * Math.min(Math.max(0, streak - 1), MAX_STREAK_BONUS_DAYS);
    }
}
