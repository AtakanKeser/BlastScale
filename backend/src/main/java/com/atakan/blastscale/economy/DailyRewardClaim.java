package com.atakan.blastscale.economy;

import jakarta.persistence.Column;
import jakarta.persistence.Embeddable;
import jakarta.persistence.EmbeddedId;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;

import java.io.Serializable;
import java.time.Instant;
import java.time.LocalDate;
import java.util.Objects;

/** One row per (player, UTC day): the primary key alone makes a second claim impossible. */
@Entity
@Table(name = "daily_reward_claim")
public class DailyRewardClaim {

    @Embeddable
    public static class Id implements Serializable {
        @Column(name = "player_id", nullable = false)
        private Long playerId;

        @Column(name = "claimed_on", nullable = false)
        private LocalDate claimedOn;

        protected Id() {
        }

        public Id(Long playerId, LocalDate claimedOn) {
            this.playerId = playerId;
            this.claimedOn = claimedOn;
        }

        public LocalDate getClaimedOn() {
            return claimedOn;
        }

        @Override
        public boolean equals(Object o) {
            return o instanceof Id other && Objects.equals(playerId, other.playerId) && Objects.equals(claimedOn, other.claimedOn);
        }

        @Override
        public int hashCode() {
            return Objects.hash(playerId, claimedOn);
        }
    }

    @EmbeddedId
    private Id id;

    @Column(name = "streak", nullable = false)
    private int streak;

    @Column(name = "coins", nullable = false)
    private int coins;

    @Column(name = "claimed_at", nullable = false)
    private Instant claimedAt;

    protected DailyRewardClaim() {
    }

    public DailyRewardClaim(Long playerId, LocalDate claimedOn, int streak, int coins, Instant claimedAt) {
        this.id = new Id(playerId, claimedOn);
        this.streak = streak;
        this.coins = coins;
        this.claimedAt = claimedAt;
    }

    public Id getId() {
        return id;
    }

    public int getStreak() {
        return streak;
    }

    public int getCoins() {
        return coins;
    }
}
