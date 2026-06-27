package com.atakan.blastscale.leaderboard;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

import java.time.Instant;

/** Marks a season as finalized; its existence is what makes the finalization job idempotent. */
@Entity
@Table(name = "leaderboard_season")
public class LeaderboardSeasonRecord {

    @Id
    @Column(name = "season", length = 10)
    private String season;

    @Column(name = "finalized_at", nullable = false)
    private Instant finalizedAt;

    @Column(name = "participants", nullable = false)
    private int participants;

    @Column(name = "rewarded_players", nullable = false)
    private int rewardedPlayers;

    protected LeaderboardSeasonRecord() {
    }

    public LeaderboardSeasonRecord(String season, Instant finalizedAt, int participants, int rewardedPlayers) {
        this.season = season;
        this.finalizedAt = finalizedAt;
        this.participants = participants;
        this.rewardedPlayers = rewardedPlayers;
    }

    public String getSeason() {
        return season;
    }

    public Instant getFinalizedAt() {
        return finalizedAt;
    }

    public int getParticipants() {
        return participants;
    }

    public int getRewardedPlayers() {
        return rewardedPlayers;
    }
}
