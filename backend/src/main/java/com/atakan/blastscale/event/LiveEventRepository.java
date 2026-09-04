package com.atakan.blastscale.event;

import org.springframework.data.jpa.repository.JpaRepository;

import java.time.Instant;
import java.util.Collection;
import java.util.List;

public interface LiveEventRepository extends JpaRepository<LiveEvent, Long> {

    List<LiveEvent> findByStatusIn(Collection<LiveEventStatus> statuses);

    List<LiveEvent> findByStatusAndStartAtLessThanEqual(LiveEventStatus status, Instant now);

    List<LiveEvent> findByStatusAndEndAtLessThanEqual(LiveEventStatus status, Instant now);

    List<LiveEvent> findAllByOrderByIdDesc();
}
