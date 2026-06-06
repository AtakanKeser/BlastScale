package com.atakan.blastscale.telemetry;

import com.atakan.blastscale.common.metrics.GameplayMetrics;
import com.atakan.blastscale.common.redis.DistributedLock;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.transaction.support.TransactionTemplate;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.List;

/**
 * Background worker of the transactional outbox.
 *
 * <pre>
 *   outbox_event (MySQL)  --lock batch (SKIP LOCKED)-->  TelemetryPublisher  -->  Elasticsearch
 *                          <-- mark published / attempts++ --
 * </pre>
 *
 * <p>Failures are retried on the next tick with a bounded number of attempts; rows that keep
 * failing stay in the table for inspection instead of being dropped. Publishing happens while the
 * rows are locked, so a crash between "published" and "marked" leads to the same events being
 * indexed twice — Elasticsearch document ids equal the outbox ids, which makes that harmless.
 */
@Component
public class OutboxPublisherJob {

    private static final Logger log = LoggerFactory.getLogger(OutboxPublisherJob.class);

    private final OutboxEventRepository outbox;
    private final TelemetryPublisher publisher;
    private final OutboxProperties properties;
    private final GameplayMetrics metrics;
    private final DistributedLock lock;
    private final Clock clock;
    private final TransactionTemplate transaction;

    public OutboxPublisherJob(OutboxEventRepository outbox, TelemetryPublisher publisher, OutboxProperties properties,
                              GameplayMetrics metrics, DistributedLock lock, Clock clock,
                              PlatformTransactionManager transactionManager) {
        this.outbox = outbox;
        this.publisher = publisher;
        this.properties = properties;
        this.metrics = metrics;
        this.lock = lock;
        this.clock = clock;
        // Programmatic transactions: a @Transactional method called from tick() on the same bean
        // would bypass the Spring proxy (self-invocation) and silently run without a transaction,
        // which means the SKIP LOCKED claim and the "published" flag would never be flushed.
        this.transaction = new TransactionTemplate(transactionManager);
    }

    @Scheduled(fixedDelayString = "${blastscale.outbox.poll-interval}", initialDelayString = "5s")
    public void tick() {
        try {
            int published;
            do {
                published = publishBatch();
            } while (published == properties.batchSize()); // drain a backlog quickly
            metrics.outboxPending(outbox.countByPublishedAtIsNull());
        } catch (RuntimeException e) {
            // Typically MySQL being unavailable; nothing to do but wait for the next tick.
            log.warn("Outbox tick failed: {}", e.getMessage());
        }
    }

    /** @return number of events published in this batch */
    public int publishBatch() {
        Integer published = transaction.execute(status -> {
            List<OutboxEvent> batch = outbox.lockNextBatch(properties.batchSize(), properties.maxAttempts());
            if (batch.isEmpty()) {
                return 0;
            }
            try {
                publisher.publish(batch);
                Instant now = Instant.now(clock);
                batch.forEach(event -> event.markPublished(now));
                metrics.outboxPublished(batch.size());
                return batch.size();
            } catch (RuntimeException e) {
                log.warn("Publishing {} outbox events failed (will retry): {}", batch.size(), e.getMessage());
                batch.forEach(event -> event.markFailed(e.getMessage()));
                metrics.outboxFailed(batch.size());
                return 0;
            }
        });
        return published == null ? 0 : published;
    }

    /** Daily housekeeping: published rows older than a week are no longer needed in MySQL. */
    @Scheduled(cron = "0 30 3 * * *")
    @Transactional
    public void purgePublished() {
        lock.withLock("outbox-purge", Duration.ofMinutes(10), () -> {
            int deleted = outbox.deletePublishedBefore(Instant.now(clock).minus(Duration.ofDays(7)));
            log.info("Purged {} published outbox events", deleted);
            return deleted;
        });
    }
}
