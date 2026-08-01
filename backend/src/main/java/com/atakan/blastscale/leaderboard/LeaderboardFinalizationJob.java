package com.atakan.blastscale.leaderboard;

import com.atakan.blastscale.common.redis.DistributedLock;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;

/**
 * Closes the previous season shortly after the week rolls over. Runs on every replica, but the
 * Redis lock ensures only one of them does the work; the others simply find nothing to do.
 */
@Component
@ConditionalOnProperty(prefix = "blastscale.jobs", name = "enabled", havingValue = "true", matchIfMissing = true)
public class LeaderboardFinalizationJob {

    private static final Logger log = LoggerFactory.getLogger(LeaderboardFinalizationJob.class);

    private final LeaderboardService leaderboardService;
    private final LeaderboardSeasonRepository seasons;
    private final DistributedLock lock;
    private final Clock clock;

    public LeaderboardFinalizationJob(LeaderboardService leaderboardService, LeaderboardSeasonRepository seasons,
                                      DistributedLock lock, Clock clock) {
        this.leaderboardService = leaderboardService;
        this.seasons = seasons;
        this.lock = lock;
        this.clock = clock;
    }

    @Scheduled(cron = "0 */10 * * * *")
    public void finalizePreviousSeason() {
        String previous = LeaderboardSeason.previous(LeaderboardSeason.at(Instant.now(clock)));
        if (seasons.existsById(previous)) {
            return;
        }
        lock.withLock("leaderboard-finalize:" + previous, Duration.ofMinutes(5), () -> {
            try {
                return leaderboardService.finalizeSeason(previous, false);
            } catch (RuntimeException e) {
                log.warn("Leaderboard finalization of {} failed, will retry: {}", previous, e.getMessage());
                return null;
            }
        });
    }
}
