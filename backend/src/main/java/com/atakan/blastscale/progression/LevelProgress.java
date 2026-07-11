package com.atakan.blastscale.progression;

import jakarta.persistence.Column;
import jakarta.persistence.Embeddable;
import jakarta.persistence.EmbeddedId;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;

import java.io.Serializable;
import java.time.Instant;
import java.util.Objects;

/** Best result per (player, level): stars and score shown on the level map. */
@Entity
@Table(name = "level_progress")
public class LevelProgress {

    @Embeddable
    public static class Id implements Serializable {
        @Column(name = "player_id", nullable = false)
        private Long playerId;

        @Column(name = "level_id", nullable = false)
        private int levelId;

        protected Id() {
        }

        public Id(Long playerId, int levelId) {
            this.playerId = playerId;
            this.levelId = levelId;
        }

        public int getLevelId() {
            return levelId;
        }

        @Override
        public boolean equals(Object o) {
            return o instanceof Id other && Objects.equals(playerId, other.playerId) && levelId == other.levelId;
        }

        @Override
        public int hashCode() {
            return Objects.hash(playerId, levelId);
        }
    }

    @EmbeddedId
    private Id id;

    @Column(name = "stars", nullable = false)
    private int stars;

    @Column(name = "best_score", nullable = false)
    private int bestScore;

    @Column(name = "attempts", nullable = false)
    private int attempts;

    /** First successful completion; {@code null} until the level is cleared. */
    @Column(name = "completed_at")
    private Instant completedAt;

    @Column(name = "last_played_at", nullable = false)
    private Instant lastPlayedAt;

    protected LevelProgress() {
    }

    public LevelProgress(Long playerId, int levelId, Instant now) {
        this.id = new Id(playerId, levelId);
        this.lastPlayedAt = now;
    }

    public Id getId() {
        return id;
    }

    public int getStars() {
        return stars;
    }

    public int getBestScore() {
        return bestScore;
    }

    public int getAttempts() {
        return attempts;
    }

    public Instant getCompletedAt() {
        return completedAt;
    }

    public Instant getLastPlayedAt() {
        return lastPlayedAt;
    }

    public boolean isCleared() {
        return completedAt != null;
    }

    void recordAttempt(Instant now) {
        attempts++;
        lastPlayedAt = now;
    }

    /** @return stars gained compared to the previous best (0 when not improved) */
    int recordClear(int score, int stars, Instant now) {
        int gained = Math.max(0, stars - this.stars);
        this.stars = Math.max(this.stars, stars);
        this.bestScore = Math.max(this.bestScore, score);
        if (completedAt == null) {
            completedAt = now;
        }
        return gained;
    }
}
