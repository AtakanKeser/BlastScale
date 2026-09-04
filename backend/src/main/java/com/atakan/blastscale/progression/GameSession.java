package com.atakan.blastscale.progression;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

import java.time.Instant;

/**
 * One attempt at a level. Created by {@code POST /levels/{id}/start} (which also consumes a
 * life and fixes the board seed) and closed by exactly one complete/fail request.
 */
@Entity
@Table(name = "game_session")
public class GameSession {

    @Id
    @Column(name = "id", length = 36)
    private String id;

    @Column(name = "player_id", nullable = false)
    private Long playerId;

    @Column(name = "level_id", nullable = false)
    private int levelId;

    /** Board seed chosen by the server; the client cannot pick a convenient board. */
    @Column(name = "seed", nullable = false)
    private int seed;

    /** Version of the level definition the client was given, for post-mortems after a redesign. */
    @Column(name = "configuration_version", nullable = false)
    private int configurationVersion;

    @Enumerated(EnumType.STRING)
    @Column(name = "status", nullable = false, length = 16)
    private SessionStatus status = SessionStatus.ACTIVE;

    @Column(name = "started_at", nullable = false)
    private Instant startedAt;

    @Column(name = "completed_at")
    private Instant completedAt;

    @Column(name = "score")
    private Integer score;

    @Column(name = "moves_used")
    private Integer movesUsed;

    @Column(name = "stars")
    private Integer stars;

    @Column(name = "reward_coins")
    private Long rewardCoins;

    @Column(name = "reward_strategy", length = 32)
    private String rewardStrategy;

    @Column(name = "reward_multiplier")
    private Double rewardMultiplier;

    protected GameSession() {
    }

    public GameSession(String id, Long playerId, int levelId, int seed, int configurationVersion, Instant startedAt) {
        this.id = id;
        this.playerId = playerId;
        this.levelId = levelId;
        this.seed = seed;
        this.configurationVersion = configurationVersion;
        this.startedAt = startedAt;
    }

    public String getId() {
        return id;
    }

    public Long getPlayerId() {
        return playerId;
    }

    public int getLevelId() {
        return levelId;
    }

    public int getSeed() {
        return seed;
    }

    public int getConfigurationVersion() {
        return configurationVersion;
    }

    public SessionStatus getStatus() {
        return status;
    }

    public Instant getStartedAt() {
        return startedAt;
    }

    public Instant getCompletedAt() {
        return completedAt;
    }

    public Integer getScore() {
        return score;
    }

    public Integer getMovesUsed() {
        return movesUsed;
    }

    public Integer getStars() {
        return stars;
    }

    public Long getRewardCoins() {
        return rewardCoins;
    }

    public String getRewardStrategy() {
        return rewardStrategy;
    }

    public Double getRewardMultiplier() {
        return rewardMultiplier;
    }

    void recordReward(long rewardCoins, String rewardStrategy, double rewardMultiplier) {
        this.rewardCoins = rewardCoins;
        this.rewardStrategy = rewardStrategy;
        this.rewardMultiplier = rewardMultiplier;
    }
}
