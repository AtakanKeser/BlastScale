package com.atakan.blastscale.telemetry;

import java.util.List;

/**
 * Destination of outbox events. Elasticsearch is the only implementation today; the interface
 * exists so a Kafka or S3 sink could be added without touching the outbox job.
 */
public interface TelemetryPublisher {

    /** Publishes the whole batch or throws; partial success is treated as failure and retried. */
    void publish(List<OutboxEvent> events);
}
