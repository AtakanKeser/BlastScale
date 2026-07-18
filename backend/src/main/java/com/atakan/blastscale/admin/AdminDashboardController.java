package com.atakan.blastscale.admin;

import com.atakan.blastscale.event.LiveEventService;
import com.atakan.blastscale.experiment.ExperimentService;
import com.atakan.blastscale.player.PlayerRepository;
import com.atakan.blastscale.progression.validation.CompletionValidationChain;
import com.atakan.blastscale.telemetry.OutboxEventRepository;
import com.atakan.blastscale.telemetry.OutboxProperties;
import io.micrometer.core.instrument.Counter;
import io.micrometer.core.instrument.MeterRegistry;
import io.micrometer.core.instrument.Timer;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.lang.management.ManagementFactory;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.TimeUnit;

/**
 * Aggregated numbers for the LiveOps dashboard's header cards.
 *
 * <p>Business counts come from the databases; request/latency/error figures come from this
 * instance's Micrometer registry (since process start). Time-windowed rates and percentiles across
 * all replicas are what Prometheus/Grafana are for — the admin panel queries Prometheus directly
 * for those, this endpoint deliberately stays cheap and dependency-free.
 */
@RestController
@RequestMapping("/api/v1/admin/dashboard")
@PreAuthorize("hasRole('ADMIN')")
public class AdminDashboardController {

    private final PlayerRepository players;
    private final LiveEventService eventService;
    private final ExperimentService experimentService;
    private final OutboxEventRepository outbox;
    private final OutboxProperties outboxProperties;
    private final CompletionValidationChain validationChain;
    private final MeterRegistry registry;

    public AdminDashboardController(PlayerRepository players, LiveEventService eventService,
                                    ExperimentService experimentService, OutboxEventRepository outbox,
                                    OutboxProperties outboxProperties, CompletionValidationChain validationChain,
                                    MeterRegistry registry) {
        this.players = players;
        this.eventService = eventService;
        this.experimentService = experimentService;
        this.outbox = outbox;
        this.outboxProperties = outboxProperties;
        this.validationChain = validationChain;
        this.registry = registry;
    }

    @GetMapping
    public Dashboard dashboard() {
        long uptimeSeconds = ManagementFactory.getRuntimeMXBean().getUptime() / 1000;

        // ----- HTTP totals since start (all endpoints of this instance) -----
        long requests = 0;
        long serverErrors = 0;
        double totalSeconds = 0;
        double maxSeconds = 0;
        for (Timer timer : registry.find("http.server.requests").timers()) {
            long count = timer.count();
            requests += count;
            totalSeconds += timer.totalTime(TimeUnit.SECONDS);
            maxSeconds = Math.max(maxSeconds, timer.max(TimeUnit.SECONDS));
            String status = timer.getId().getTag("status");
            if (status != null && status.startsWith("5")) {
                serverErrors += count;
            }
        }

        // ----- gameplay counters -----
        Map<String, Long> completions = new LinkedHashMap<>();
        for (Counter counter : registry.find("blastscale_level_completion_total").counters()) {
            completions.put(counter.getId().getTag("result"), (long) counter.count());
        }
        Map<String, Long> rejections = new LinkedHashMap<>();
        for (Counter counter : registry.find("blastscale_completion_rejected_total").counters()) {
            rejections.put(counter.getId().getTag("validator"), (long) counter.count());
        }
        double hits = 0;
        double misses = 0;
        for (Counter counter : registry.find("blastscale_cache_requests_total").counters()) {
            String result = counter.getId().getTag("result");
            if ("hit".equals(result)) {
                hits += counter.count();
            } else if ("miss".equals(result)) {
                misses += counter.count();
            }
        }
        double levelStarts = registry.find("blastscale_level_start_total").counter() == null ? 0
                : registry.find("blastscale_level_start_total").counter().count();

        return new Dashboard(
                uptimeSeconds,
                new Http(requests, requests == 0 ? 0 : totalSeconds / requests * 1000, maxSeconds * 1000,
                        requests == 0 ? 0 : (double) serverErrors / requests),
                players.count(),
                (long) levelStarts,
                completions,
                rejections,
                hits + misses == 0 ? null : hits / (hits + misses),
                new Outbox(outbox.countByPublishedAtIsNull(),
                        outbox.countByPublishedAtIsNullAndAttemptsGreaterThanEqual(outboxProperties.maxAttempts())),
                eventService.activeEvents().size(),
                experimentService.liveExperiments().size(),
                validationChain.validatorNames());
    }

    public record Dashboard(long uptimeSeconds, Http http, long players, long levelStarts,
                            Map<String, Long> levelCompletions, Map<String, Long> completionRejections,
                            Double cacheHitRate, Outbox outbox, int activeEvents, int runningExperiments,
                            List<String> antiCheatValidators) {
    }

    public record Http(long requests, double meanLatencyMillis, double maxLatencyMillis, double serverErrorRate) {
    }

    public record Outbox(long pending, long deadLettered) {
    }
}
