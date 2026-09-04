package com.atakan.blastscale.leaderboard;

import jakarta.persistence.Column;
import jakarta.persistence.Embeddable;
import jakarta.persistence.EmbeddedId;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;

import java.io.Serializable;
import java.util.Objects;

/** Audit row: which player got which rank and prize for a season. */
@Entity
@Table(name = "leaderboard_reward")
public class LeaderboardReward {

    @Embeddable
    public static class Id implements Serializable {
        @Column(name = "season", length = 10, nullable = false)
        private String season;

        @Column(name = "player_id", nullable = false)
        private Long playerId;

        protected Id() {
        }

        public Id(String season, Long playerId) {
            this.season = season;
            this.playerId = playerId;
        }

        public Long getPlayerId() {
            return playerId;
        }

        @Override
        public boolean equals(Object o) {
            return o instanceof Id other && Objects.equals(season, other.season) && Objects.equals(playerId, other.playerId);
        }

        @Override
        public int hashCode() {
            return Objects.hash(season, playerId);
        }
    }

    @EmbeddedId
    private Id id;

    @Column(name = "rank_position", nullable = false)
    private int rank;

    @Column(name = "score", nullable = false)
    private long score;

    @Column(name = "coins", nullable = false)
    private int coins;

    protected LeaderboardReward() {
    }

    public LeaderboardReward(String season, Long playerId, int rank, long score, int coins) {
        this.id = new Id(season, playerId);
        this.rank = rank;
        this.score = score;
        this.coins = coins;
    }

    public Id getId() {
        return id;
    }

    public int getRank() {
        return rank;
    }

    public long getScore() {
        return score;
    }

    public int getCoins() {
        return coins;
    }
}
