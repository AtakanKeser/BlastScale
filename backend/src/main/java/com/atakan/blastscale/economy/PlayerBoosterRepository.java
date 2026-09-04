package com.atakan.blastscale.economy;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;
import java.util.Optional;

public interface PlayerBoosterRepository extends JpaRepository<PlayerBooster, Long> {

    List<PlayerBooster> findByPlayerId(long playerId);

    Optional<PlayerBooster> findByPlayerIdAndBoosterType(long playerId, BoosterType boosterType);
}
