package com.atakan.blastscale.event;

import com.atakan.blastscale.common.redis.DistributedLock;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

import java.time.Duration;

/** Drives event lifecycles by time. One replica does the work per tick thanks to the Redis lock. */
@Component
public class LiveEventScheduler {

    private static final Logger log = LoggerFactory.getLogger(LiveEventScheduler.class);

    private final LiveEventService eventService;
    private final DistributedLock lock;

    public LiveEventScheduler(LiveEventService eventService, DistributedLock lock) {
        this.eventService = eventService;
        this.lock = lock;
    }

    @Scheduled(fixedDelayString = "30s", initialDelayString = "20s")
    public void tick() {
        lock.withLock("live-event-scheduler", Duration.ofSeconds(25), () -> {
            try {
                int activated = eventService.activateDue();
                int ended = eventService.endDue();
                int retried = eventService.finalizePending();
                if (activated + ended + retried > 0) {
                    log.info("Live event scheduler: activated={}, ended={}, finalization retries={}", activated, ended, retried);
                }
            } catch (RuntimeException e) {
                log.warn("Live event scheduler tick failed: {}", e.getMessage());
            }
            return null;
        });
    }
}
