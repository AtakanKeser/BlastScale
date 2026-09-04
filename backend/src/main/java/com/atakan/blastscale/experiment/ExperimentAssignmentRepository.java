package com.atakan.blastscale.experiment;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.time.Instant;
import java.util.List;

public interface ExperimentAssignmentRepository extends JpaRepository<ExperimentAssignment, ExperimentAssignmentId> {

    List<ExperimentAssignment> findByIdPlayerId(long playerId);

    /**
     * Race-free first assignment. Two devices of the same player may request the config at the
     * same instant; {@code INSERT IGNORE} lets exactly one of them win and returns 0 rows for the
     * other, which then simply reads the stored row.
     */
    @Modifying
    @Query(value = """
            INSERT IGNORE INTO experiment_assignment (experiment_id, player_id, variant, bucket, assigned_at)
            VALUES (:experimentId, :playerId, :variant, :bucket, :assignedAt)
            """, nativeQuery = true)
    int insertIfAbsent(@Param("experimentId") long experimentId, @Param("playerId") long playerId,
                       @Param("variant") String variant, @Param("bucket") int bucket,
                       @Param("assignedAt") Instant assignedAt);

    @Query("select a.variant as variant, count(a) as count from ExperimentAssignment a " +
            "where a.id.experimentId = :experimentId group by a.variant")
    List<VariantCount> countByVariant(@Param("experimentId") long experimentId);

    /** Projection for the admin panel's distribution view. */
    interface VariantCount {
        String getVariant();

        long getCount();
    }
}
