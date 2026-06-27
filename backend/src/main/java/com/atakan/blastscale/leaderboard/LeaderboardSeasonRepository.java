package com.atakan.blastscale.leaderboard;

import org.springframework.data.jpa.repository.JpaRepository;

public interface LeaderboardSeasonRepository extends JpaRepository<LeaderboardSeasonRecord, String> {
}
