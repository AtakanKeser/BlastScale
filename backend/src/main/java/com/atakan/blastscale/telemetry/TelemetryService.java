package com.atakan.blastscale.telemetry;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;
import tools.jackson.databind.ObjectMapper;

import java.time.Clock;
import java.time.Instant;
import java.util.Map;

/**
 * Entry point other modules use to record telemetry.
 *
 * <p>{@link #record} only writes an outbox row. With {@code REQUIRED} propagation it joins the
 * caller's transaction when there is one — which is the whole point of the outbox pattern — and
 * opens a short one otherwise.
 */
@Service
public class TelemetryService {

    private final OutboxEventRepository outbox;
    private final ObjectMapper objectMapper;
    private final Clock clock;

    public TelemetryService(OutboxEventRepository outbox, ObjectMapper objectMapper, Clock clock) {
        this.outbox = outbox;
        this.objectMapper = objectMapper;
        this.clock = clock;
    }

    @Transactional(propagation = Propagation.REQUIRED)
    public void record(TelemetryEventType type, Long playerId, String aggregateType, String aggregateId,
                       Map<String, ?> payload) {
        String json = objectMapper.writeValueAsString(payload == null ? Map.of() : payload);
        outbox.save(new OutboxEvent(type, playerId, aggregateType, aggregateId, json, Instant.now(clock)));
    }
}
