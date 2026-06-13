package com.atakan.blastscale.economy;

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
 * Append-only ledger entry. Balances in {@link Wallet} are a cache of the sum of these rows;
 * the ledger is what support, fraud analysis and reconciliation look at.
 */
@Entity
@Table(name = "economy_transaction")
public class EconomyTransaction {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(name = "player_id", nullable = false)
    private Long playerId;

    @Enumerated(EnumType.STRING)
    @Column(name = "type", nullable = false, length = 8)
    private TransactionType type;

    @Enumerated(EnumType.STRING)
    @Column(name = "resource", nullable = false, length = 32)
    private Resource resource;

    /** Signed: positive for credits, negative for debits. */
    @Column(name = "amount", nullable = false)
    private long amount;

    /** Balance of the resource right after this entry, for quick investigations. */
    @Column(name = "balance_after", nullable = false)
    private long balanceAfter;

    @Enumerated(EnumType.STRING)
    @Column(name = "reason", nullable = false, length = 32)
    private TransactionReason reason;

    /** What caused the entry: session id, ISO date of a daily reward, season id, ... */
    @Column(name = "reference_id", nullable = false, length = 64)
    private String referenceId;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt;

    protected EconomyTransaction() {
        // JPA
    }

    public EconomyTransaction(Long playerId, Resource resource, long amount, long balanceAfter,
                              TransactionReason reason, String referenceId, Instant createdAt) {
        this.playerId = playerId;
        this.type = amount >= 0 ? TransactionType.CREDIT : TransactionType.DEBIT;
        this.resource = resource;
        this.amount = amount;
        this.balanceAfter = balanceAfter;
        this.reason = reason;
        this.referenceId = referenceId;
        this.createdAt = createdAt;
    }

    public Long getId() {
        return id;
    }

    public Long getPlayerId() {
        return playerId;
    }

    public TransactionType getType() {
        return type;
    }

    public Resource getResource() {
        return resource;
    }

    public long getAmount() {
        return amount;
    }

    public long getBalanceAfter() {
        return balanceAfter;
    }

    public TransactionReason getReason() {
        return reason;
    }

    public String getReferenceId() {
        return referenceId;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }
}
