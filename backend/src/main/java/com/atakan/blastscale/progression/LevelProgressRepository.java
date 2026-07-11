package com.atakan.blastscale.progression;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;

public interface LevelProgressRepository extends JpaRepository<LevelProgress, LevelProgress.Id> {

    List<LevelProgress> findByIdPlayerIdOrderByIdLevelIdAsc(long playerId);
}
