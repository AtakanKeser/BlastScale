package com.atakan.blastscale.telemetry;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.elasticsearch.core.ElasticsearchOperations;
import org.springframework.data.elasticsearch.core.IndexOperations;
import org.springframework.stereotype.Component;
import tools.jackson.core.type.TypeReference;
import tools.jackson.databind.ObjectMapper;

import java.util.List;
import java.util.Map;

/** Bulk-indexes outbox rows into Elasticsearch, creating the index with its mapping on first use. */
@Component
public class ElasticsearchTelemetryPublisher implements TelemetryPublisher {

    private static final Logger log = LoggerFactory.getLogger(ElasticsearchTelemetryPublisher.class);
    private static final TypeReference<Map<String, Object>> MAP = new TypeReference<>() {
    };

    private final ElasticsearchOperations elasticsearch;
    private final ObjectMapper objectMapper;
    private volatile boolean indexReady;

    public ElasticsearchTelemetryPublisher(ElasticsearchOperations elasticsearch, ObjectMapper objectMapper) {
        this.elasticsearch = elasticsearch;
        this.objectMapper = objectMapper;
    }

    @Override
    public void publish(List<OutboxEvent> events) {
        ensureIndex();
        List<TelemetryDocument> documents = events.stream().map(this::toDocument).toList();
        elasticsearch.save(documents);
    }

    private void ensureIndex() {
        if (indexReady) {
            return;
        }
        IndexOperations indexOps = elasticsearch.indexOps(TelemetryDocument.class);
        if (!indexOps.exists()) {
            indexOps.create();
            indexOps.putMapping(indexOps.createMapping());
            log.info("Created Elasticsearch index {}", TelemetryDocument.INDEX);
        }
        indexReady = true;
    }

    private TelemetryDocument toDocument(OutboxEvent event) {
        Map<String, Object> payload = objectMapper.readValue(event.getPayload(), MAP);
        return new TelemetryDocument(Long.toString(event.getId()), event.getEventType().name(), event.getPlayerId(),
                event.getAggregateType(), event.getAggregateId(), event.getCreatedAt(), payload);
    }
}
