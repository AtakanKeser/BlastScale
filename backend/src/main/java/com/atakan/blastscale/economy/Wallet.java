package com.atakan.blastscale.economy;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.Version;

import java.time.Instant;

/**
 * Current balances of a player. Always modified under a row lock (see
 * {@link WalletRepository#lockByPlayerId}) and never without a matching {@link EconomyTransaction}.
 *
 * <p>{@code version} adds optimistic locking on top of the pessimistic lock: if any code path ever
 * modifies a wallet outside the lock, the stale write fails instead of silently overwriting.
 */
@Entity
@Table(name = "player_wallet")
public class Wallet {

    @Id
    @Column(name = "player_id")
    private Long playerId;

    @Column(name = "coins", nullable = false)
    private long coins;

    @Column(name = "lives", nullable = false)
    private int lives;

    @Column(name = "stars", nullable = false)
    private int stars;

    /** Reference point for life regeneration (see {@link LifeRegeneration}). */
    @Column(name = "lives_updated_at", nullable = false)
    private Instant livesUpdatedAt;

    @Version
    @Column(name = "version", nullable = false)
    private long version;

    protected Wallet() {
        // JPA
    }

    public Wallet(Long playerId, long coins, int lives, Instant now) {
        this.playerId = playerId;
        this.coins = coins;
        this.lives = lives;
        this.livesUpdatedAt = now;
    }

    public Long getPlayerId() {
        return playerId;
    }

    public long getCoins() {
        return coins;
    }

    public int getLives() {
        return lives;
    }

    public int getStars() {
        return stars;
    }

    public Instant getLivesUpdatedAt() {
        return livesUpdatedAt;
    }

    public long getVersion() {
        return version;
    }

    void setCoins(long coins) {
        this.coins = coins;
    }

    void setStars(int stars) {
        this.stars = stars;
    }

    void setLives(int lives, Instant livesUpdatedAt) {
        this.lives = lives;
        this.livesUpdatedAt = livesUpdatedAt;
    }
}
