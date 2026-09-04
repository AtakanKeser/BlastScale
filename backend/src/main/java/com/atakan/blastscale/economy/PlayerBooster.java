package com.atakan.blastscale.economy;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

/** Booster inventory row; one per (player, booster type). Modified only under the wallet lock. */
@Entity
@Table(name = "player_booster")
public class PlayerBooster {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(name = "player_id", nullable = false)
    private Long playerId;

    @Enumerated(EnumType.STRING)
    @Column(name = "booster_type", nullable = false, length = 32)
    private BoosterType boosterType;

    @Column(name = "quantity", nullable = false)
    private int quantity;

    protected PlayerBooster() {
        // JPA
    }

    public PlayerBooster(Long playerId, BoosterType boosterType) {
        this.playerId = playerId;
        this.boosterType = boosterType;
    }

    public BoosterType getBoosterType() {
        return boosterType;
    }

    public int getQuantity() {
        return quantity;
    }

    void setQuantity(int quantity) {
        this.quantity = quantity;
    }
}
