package com.atakan.blastscale.player;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.Optional;

public interface PlayerRepository extends JpaRepository<Player, Long> {

    Optional<Player> findByUsername(String username);

    Optional<Player> findByDeviceId(String deviceId);

    boolean existsByUsername(String username);

    Page<Player> findByUsernameContainingIgnoreCaseOrderByIdDesc(String query, Pageable pageable);
}
