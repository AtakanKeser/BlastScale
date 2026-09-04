package com.atakan.blastscale.telemetry;

import org.springframework.format.annotation.DateTimeFormat;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import java.time.Instant;

/** Operational investigation endpoints used by the admin panel. */
@RestController
@RequestMapping("/api/v1/admin")
@PreAuthorize("hasRole('ADMIN')")
public class TelemetryAdminController {

    private final TelemetrySearchService search;
    private final OutboxEventRepository outbox;
    private final OutboxProperties outboxProperties;

    public TelemetryAdminController(TelemetrySearchService search, OutboxEventRepository outbox,
                                    OutboxProperties outboxProperties) {
        this.search = search;
        this.outbox = outbox;
        this.outboxProperties = outboxProperties;
    }

    /** {@code GET /api/v1/admin/players/123/events?type=LEVEL_COMPLETED&from=...&to=...} */
    @GetMapping("/players/{playerId}/events")
    public TelemetrySearchService.EventPage playerEvents(
            @PathVariable long playerId,
            @RequestParam(required = false) TelemetryEventType type,
            @RequestParam(required = false) @DateTimeFormat(iso = DateTimeFormat.ISO.DATE_TIME) Instant from,
            @RequestParam(required = false) @DateTimeFormat(iso = DateTimeFormat.ISO.DATE_TIME) Instant to,
            @RequestParam(defaultValue = "0") int page,
            @RequestParam(defaultValue = "50") int size) {
        return search.playerEvents(playerId, type, from, to, page, Math.min(size, 200));
    }

    /** Outbox backlog, useful to spot an Elasticsearch outage from the dashboard. */
    @GetMapping("/telemetry/outbox")
    public OutboxStats outboxStats() {
        return new OutboxStats(outbox.countByPublishedAtIsNull(),
                outbox.countByPublishedAtIsNullAndAttemptsGreaterThanEqual(outboxProperties.maxAttempts()));
    }

    public record OutboxStats(long pending, long deadLettered) {
    }
}
