package com.atakan.blastscale.event;

import jakarta.persistence.Column;
import jakarta.persistence.Embeddable;
import jakarta.persistence.EmbeddedId;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;

import java.io.Serializable;
import java.time.Instant;
import java.util.Objects;

/**
 * A player's standing in one event. Points are accumulated with an atomic
 * {@code INSERT ... ON DUPLICATE KEY UPDATE} inside the level-completion transaction, so event
 * progress can never disagree with the progression and ledger rows written alongside it.
 * Final rank and prize are filled in at finalization.
 */
@Entity
@Table(name = "live_event_participation")
public class EventParticipation {

    @Embeddable
    public static class Id implements Serializable {
        @Column(name = "event_id", nullable = false)
        private Long eventId;

        @Column(name = "player_id", nullable = false)
        private Long playerId;

        protected Id() {
        }

        public Id(Long eventId, Long playerId) {
            this.eventId = eventId;
            this.playerId = playerId;
        }

        public Long getEventId() {
            return eventId;
        }

        public Long getPlayerId() {
            return playerId;
        }

        @Override
        public boolean equals(Object o) {
            return o instanceof Id other && Objects.equals(eventId, other.eventId) && Objects.equals(playerId, other.playerId);
        }

        @Override
        public int hashCode() {
            return Objects.hash(eventId, playerId);
        }
    }

    @EmbeddedId
    private Id id;

    @Column(name = "points", nullable = false)
    private long points;

    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;

    @Column(name = "final_rank")
    private Integer finalRank;

    @Column(name = "reward_coins")
    private Integer rewardCoins;

    protected EventParticipation() {
    }

    public Id getId() {
        return id;
    }

    public long getPoints() {
        return points;
    }

    public Instant getUpdatedAt() {
        return updatedAt;
    }

    public Integer getFinalRank() {
        return finalRank;
    }

    public Integer getRewardCoins() {
        return rewardCoins;
    }

    void setFinalResult(int rank, int coins) {
        this.finalRank = rank;
        this.rewardCoins = coins;
    }
}
