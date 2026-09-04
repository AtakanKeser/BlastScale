package com.atakan.blastscale.common.metrics;

import io.micrometer.core.instrument.Counter;
import io.micrometer.core.instrument.MeterRegistry;
import io.micrometer.core.instrument.Timer;
import org.springframework.stereotype.Component;

import java.time.Duration;
import java.util.concurrent.atomic.AtomicLong;

/**
 * Central place for the custom Micrometer metrics exported at {@code /actuator/prometheus}.
 *
 * <p>Naming follows Prometheus conventions (snake_case, {@code _total} suffix for counters).
 * Keeping every metric name in one class makes the Grafana dashboard easy to keep in sync with
 * the code.
 */
@Component
public class GameplayMetrics {

    private final MeterRegistry registry;
    private final AtomicLong outboxPending = new AtomicLong();

    public GameplayMetrics(MeterRegistry registry) {
        this.registry = registry;
        registry.gauge("blastscale_outbox_pending", outboxPending);
    }

    /** A level session was created. */
    public void levelStarted() {
        registry.counter("blastscale_level_start_total").increment();
    }

    /** Result of a completion request: {@code success}, {@code rejected}, {@code replayed}, {@code failed}. */
    public void levelCompletion(String result) {
        Counter.builder("blastscale_level_completion_total")
                .tag("result", result)
                .register(registry)
                .increment();
    }

    /** Anti-cheat validator that rejected a completion, for spotting cheat patterns. */
    public void completionRejected(String validator) {
        Counter.builder("blastscale_completion_rejected_total")
                .tag("validator", validator)
                .register(registry)
                .increment();
    }

    /** How long the whole "validate -> reward -> persist" pipeline took. */
    public void rewardProcessing(Duration duration) {
        Timer.builder("blastscale_reward_processing_duration")
                .publishPercentileHistogram()
                .register(registry)
                .record(duration);
    }

    /** Every ledger entry, split by resource and direction. */
    public void economyTransaction(String resource, String type) {
        Counter.builder("blastscale_economy_transaction_total")
                .tag("resource", resource)
                .tag("type", type)
                .register(registry)
                .increment();
    }

    /** Cache-aside outcome: {@code hit}, {@code miss} or {@code error} (Redis unavailable). */
    public void cacheAccess(String cache, String result) {
        Counter.builder("blastscale_cache_requests_total")
                .tag("cache", cache)
                .tag("result", result)
                .register(registry)
                .increment();
    }

    /** A request was replayed from the idempotency store instead of being executed again. */
    public void idempotentReplay(String scope) {
        Counter.builder("blastscale_idempotent_replay_total")
                .tag("scope", scope)
                .register(registry)
                .increment();
    }

    public void rateLimitRejected() {
        registry.counter("blastscale_rate_limit_rejected_total").increment();
    }

    public void outboxPublished(int count) {
        registry.counter("blastscale_outbox_published_total").increment(count);
    }

    public void outboxFailed(int count) {
        registry.counter("blastscale_outbox_failed_total").increment(count);
    }

    public void outboxPending(long pending) {
        outboxPending.set(pending);
    }
}
