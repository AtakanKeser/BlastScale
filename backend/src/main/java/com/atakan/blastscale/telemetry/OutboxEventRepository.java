package com.atakan.blastscale.telemetry;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.time.Instant;
import java.util.List;

public interface OutboxEventRepository extends JpaRepository<OutboxEvent, Long> {

    /**
     * Claims the next batch of unpublished rows. {@code FOR UPDATE SKIP LOCKED} lets several API
     * replicas run the publisher concurrently: each one locks a disjoint set of rows instead of
     * blocking on (or double-publishing) the rows another replica is working on.
     */
    @Query(value = """
            SELECT * FROM outbox_event
            WHERE published_at IS NULL AND attempts < :maxAttempts
            ORDER BY id
            LIMIT :batchSize
            FOR UPDATE SKIP LOCKED
            """, nativeQuery = true)
    List<OutboxEvent> lockNextBatch(@Param("batchSize") int batchSize, @Param("maxAttempts") int maxAttempts);

    long countByPublishedAtIsNull();

    long countByPublishedAtIsNullAndAttemptsGreaterThanEqual(int attempts);

    @Modifying
    @Query("delete from OutboxEvent e where e.publishedAt is not null and e.publishedAt < :before")
    int deletePublishedBefore(@Param("before") Instant before);
}
