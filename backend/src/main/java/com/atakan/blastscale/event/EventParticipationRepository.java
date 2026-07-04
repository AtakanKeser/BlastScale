package com.atakan.blastscale.event;

import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.time.Instant;
import java.util.List;

public interface EventParticipationRepository extends JpaRepository<EventParticipation, EventParticipation.Id> {

    /** Atomic upsert: no read-modify-write race between two devices of the same player. */
    @Modifying
    @Query(value = """
            INSERT INTO live_event_participation (event_id, player_id, points, updated_at)
            VALUES (:eventId, :playerId, :delta, :now)
            ON DUPLICATE KEY UPDATE points = points + VALUES(points), updated_at = VALUES(updated_at)
            """, nativeQuery = true)
    void addPoints(@Param("eventId") long eventId, @Param("playerId") long playerId,
                   @Param("delta") long delta, @Param("now") Instant now);

    /** Ranking: most points first; on ties the player who got there first wins. */
    List<EventParticipation> findByIdEventIdOrderByPointsDescUpdatedAtAsc(long eventId, Pageable pageable);

    long countByIdEventId(long eventId);

    /** 1-based rank = players strictly ahead + 1 (ties broken by time, as in the ordering above). */
    @Query("""
            select count(p) + 1 from EventParticipation p
            where p.id.eventId = :eventId
              and (p.points > :points or (p.points = :points and p.updatedAt < :updatedAt))
            """)
    long rankOf(@Param("eventId") long eventId, @Param("points") long points, @Param("updatedAt") Instant updatedAt);
}
