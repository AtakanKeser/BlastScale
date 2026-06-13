package com.atakan.blastscale.economy;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.Optional;

public interface DailyRewardClaimRepository extends JpaRepository<DailyRewardClaim, DailyRewardClaim.Id> {

    Optional<DailyRewardClaim> findTopByIdPlayerIdOrderByIdClaimedOnDesc(long playerId);
}
