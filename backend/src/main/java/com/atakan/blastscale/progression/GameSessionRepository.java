package com.atakan.blastscale.progression;

import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.time.Instant;
import java.util.List;
import java.util.Optional;

public interface GameSessionRepository extends JpaRepository<GameSession, String> {

    Optional<GameSession> findByIdAndPlayerId(String id, long playerId);

    List<GameSession> findByPlayerIdOrderByStartedAtDesc(long playerId, Pageable pageable);

    /**
     * The atomic "claim": moves the session out of ACTIVE and stores the outcome in one statement.
     * Returns 1 for exactly one caller and 0 for everybody else — this is what makes 100 concurrent
     * completions of the same session pay one reward, with or without Redis.
     */
    @Modifying(clearAutomatically = true, flushAutomatically = true)
    @Query("""
            update GameSession s
               set s.status = :status, s.completedAt = :now, s.score = :score, s.movesUsed = :movesUsed, s.stars = :stars
             where s.id = :id and s.status = com.atakan.blastscale.progression.SessionStatus.ACTIVE
            """)
    int closeIfActive(@Param("id") String id, @Param("status") SessionStatus status, @Param("now") Instant now,
                      @Param("score") int score, @Param("movesUsed") int movesUsed, @Param("stars") int stars);

    /** Any other ACTIVE session of the player is abandoned when a new level starts. */
    @Modifying(clearAutomatically = true, flushAutomatically = true)
    @Query("""
            update GameSession s set s.status = com.atakan.blastscale.progression.SessionStatus.ABANDONED
             where s.playerId = :playerId and s.status = com.atakan.blastscale.progression.SessionStatus.ACTIVE
            """)
    int abandonActive(@Param("playerId") long playerId);

    /** Housekeeping: sessions that were never closed (app killed mid-level). */
    @Modifying(clearAutomatically = true, flushAutomatically = true)
    @Query("""
            update GameSession s set s.status = com.atakan.blastscale.progression.SessionStatus.EXPIRED
             where s.status = com.atakan.blastscale.progression.SessionStatus.ACTIVE and s.startedAt < :cutoff
            """)
    int expireOlderThan(@Param("cutoff") Instant cutoff);
}
