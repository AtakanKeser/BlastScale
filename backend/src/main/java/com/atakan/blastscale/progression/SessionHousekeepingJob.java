package com.atakan.blastscale.progression;

import com.atakan.blastscale.common.redis.DistributedLock;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

import java.time.Duration;

/** Marks sessions abandoned mid-level (app killed, phone died) as EXPIRED so tables stay tidy. */
@Component
public class SessionHousekeepingJob {

    private static final Logger log = LoggerFactory.getLogger(SessionHousekeepingJob.class);

    private final ProgressionService progressionService;
    private final DistributedLock lock;

    public SessionHousekeepingJob(ProgressionService progressionService, DistributedLock lock) {
        this.progressionService = progressionService;
        this.lock = lock;
    }

    @Scheduled(fixedDelayString = "10m", initialDelayString = "2m")
    public void expireStaleSessions() {
        lock.withLock("session-housekeeping", Duration.ofMinutes(5), () -> {
            int expired = progressionService.expireStaleSessions();
            if (expired > 0) {
                log.info("Expired {} stale game sessions", expired);
            }
            return expired;
        });
    }
}
