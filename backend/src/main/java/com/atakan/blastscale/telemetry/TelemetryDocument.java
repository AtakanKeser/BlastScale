package com.atakan.blastscale.telemetry;

import org.springframework.data.annotation.Id;
import org.springframework.data.elasticsearch.annotations.DateFormat;
import org.springframework.data.elasticsearch.annotations.Document;
import org.springframework.data.elasticsearch.annotations.Field;
import org.springframework.data.elasticsearch.annotations.FieldType;

import java.time.Instant;
import java.util.Map;

/**
 * Shape of one telemetry event in the {@code blastscale-events} index.
 *
 * <p>{@code createIndex = false}: the index is created lazily by the publisher (see
 * {@link ElasticsearchTelemetryPublisher}) so that an unreachable Elasticsearch never prevents the
 * API from starting. The payload uses the {@code flattened} type: arbitrary keys are searchable
 * without risking mapping conflicts between events that reuse a field name with different types.
 */
@Document(indexName = TelemetryDocument.INDEX, createIndex = false)
public class TelemetryDocument {

    public static final String INDEX = "blastscale-events";

    @Id
    private String id;

    @Field(type = FieldType.Keyword)
    private String eventType;

    @Field(type = FieldType.Long)
    private Long playerId;

    @Field(type = FieldType.Keyword)
    private String aggregateType;

    @Field(type = FieldType.Keyword)
    private String aggregateId;

    @Field(type = FieldType.Date, format = {DateFormat.date_time, DateFormat.epoch_millis})
    private Instant timestamp;

    @Field(type = FieldType.Flattened)
    private Map<String, Object> payload;

    public TelemetryDocument() {
    }

    public TelemetryDocument(String id, String eventType, Long playerId, String aggregateType, String aggregateId,
                             Instant timestamp, Map<String, Object> payload) {
        this.id = id;
        this.eventType = eventType;
        this.playerId = playerId;
        this.aggregateType = aggregateType;
        this.aggregateId = aggregateId;
        this.timestamp = timestamp;
        this.payload = payload;
    }

    public String getId() {
        return id;
    }

    public String getEventType() {
        return eventType;
    }

    public Long getPlayerId() {
        return playerId;
    }

    public String getAggregateType() {
        return aggregateType;
    }

    public String getAggregateId() {
        return aggregateId;
    }

    public Instant getTimestamp() {
        return timestamp;
    }

    public Map<String, Object> getPayload() {
        return payload;
    }
}
