package com.atakan.blastscale.player;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

import java.time.Instant;

/**
 * A player account. Guests are players without a password whose identity is their device id.
 *
 * <p>{@code currentLevel} is the highest level the player is allowed to start; it is the only
 * progression field kept here because it is read on every level start. Detailed per-level results
 * live in the progression module.
 */
@Entity
@Table(name = "players")
public class Player {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(name = "username", nullable = false, length = 32)
    private String username;

    /** BCrypt hash; {@code null} for guest accounts. */
    @Column(name = "password_hash", length = 100)
    private String passwordHash;

    /** Device identifier for guest login; {@code null} for registered accounts. */
    @Column(name = "device_id", length = 128)
    private String deviceId;

    @Enumerated(EnumType.STRING)
    @Column(name = "role", nullable = false, length = 16)
    private PlayerRole role = PlayerRole.PLAYER;

    @Column(name = "current_level", nullable = false)
    private int currentLevel = 1;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt;

    @Column(name = "last_seen_at", nullable = false)
    private Instant lastSeenAt;

    protected Player() {
        // JPA
    }

    public Player(String username, String passwordHash, String deviceId, PlayerRole role, Instant now) {
        this.username = username;
        this.passwordHash = passwordHash;
        this.deviceId = deviceId;
        this.role = role;
        this.createdAt = now;
        this.lastSeenAt = now;
    }

    public Long getId() {
        return id;
    }

    public String getUsername() {
        return username;
    }

    public String getPasswordHash() {
        return passwordHash;
    }

    public String getDeviceId() {
        return deviceId;
    }

    public PlayerRole getRole() {
        return role;
    }

    public int getCurrentLevel() {
        return currentLevel;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }

    public Instant getLastSeenAt() {
        return lastSeenAt;
    }

    public void touch(Instant now) {
        this.lastSeenAt = now;
    }

    /** Unlocks the next level once {@code completedLevel} was the current frontier. */
    public boolean advanceIfCurrent(int completedLevel) {
        if (completedLevel == currentLevel) {
            currentLevel = completedLevel + 1;
            return true;
        }
        return false;
    }

    public void setCurrentLevel(int currentLevel) {
        this.currentLevel = currentLevel;
    }
}
