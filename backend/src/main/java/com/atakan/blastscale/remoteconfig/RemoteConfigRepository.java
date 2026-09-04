package com.atakan.blastscale.remoteconfig;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;

public interface RemoteConfigRepository extends JpaRepository<RemoteConfigEntry, String> {

    List<RemoteConfigEntry> findAllByOrderByKeyAsc();
}
