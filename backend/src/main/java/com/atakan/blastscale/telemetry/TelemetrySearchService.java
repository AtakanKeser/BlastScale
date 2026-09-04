package com.atakan.blastscale.telemetry;

import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Sort;
import org.springframework.data.elasticsearch.core.ElasticsearchOperations;
import org.springframework.data.elasticsearch.core.SearchHit;
import org.springframework.data.elasticsearch.core.SearchHits;
import org.springframework.data.elasticsearch.core.query.Criteria;
import org.springframework.data.elasticsearch.core.query.CriteriaQuery;
import org.springframework.stereotype.Service;

import java.time.Instant;
import java.util.List;

/**
 * Read side of telemetry: lets support reconstruct what happened to a player
 * ("I finished level 412 but got no reward") by filtering the Elasticsearch index.
 */
@Service
public class TelemetrySearchService {

    private final ElasticsearchOperations elasticsearch;

    public TelemetrySearchService(ElasticsearchOperations elasticsearch) {
        this.elasticsearch = elasticsearch;
    }

    public EventPage playerEvents(long playerId, TelemetryEventType type, Instant from, Instant to, int page, int size) {
        Criteria criteria = new Criteria("playerId").is(playerId);
        if (type != null) {
            criteria = criteria.and(new Criteria("eventType").is(type.name()));
        }
        if (from != null) {
            criteria = criteria.and(new Criteria("timestamp").greaterThanEqual(from));
        }
        if (to != null) {
            criteria = criteria.and(new Criteria("timestamp").lessThanEqual(to));
        }
        CriteriaQuery query = new CriteriaQuery(criteria)
                .setPageable(PageRequest.of(page, size, Sort.by(Sort.Direction.DESC, "timestamp")));
        SearchHits<TelemetryDocument> hits = elasticsearch.search(query, TelemetryDocument.class);
        List<TelemetryDocument> events = hits.getSearchHits().stream().map(SearchHit::getContent).toList();
        return new EventPage(events, hits.getTotalHits(), page, size);
    }

    public record EventPage(List<TelemetryDocument> events, long total, int page, int size) {
    }
}
