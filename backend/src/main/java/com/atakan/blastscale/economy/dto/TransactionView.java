package com.atakan.blastscale.economy.dto;

import com.atakan.blastscale.economy.EconomyTransaction;

import java.time.Instant;

public record TransactionView(long id, String type, String resource, long amount, long balanceAfter,
                              String reason, String referenceId, Instant createdAt) {

    public static TransactionView from(EconomyTransaction t) {
        return new TransactionView(t.getId(), t.getType().name(), t.getResource().name(), t.getAmount(),
                t.getBalanceAfter(), t.getReason().name(), t.getReferenceId(), t.getCreatedAt());
    }
}
