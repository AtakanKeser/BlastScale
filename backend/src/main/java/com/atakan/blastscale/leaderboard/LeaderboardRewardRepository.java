package com.atakan.blastscale.leaderboard;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;

public interface LeaderboardRewardRepository extends JpaRepository<LeaderboardReward, LeaderboardReward.Id> {

    List<LeaderboardReward> findByIdSeasonOrderByRankAsc(String season);
}
