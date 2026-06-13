package com.atakan.blastscale.economy.dto;

import com.atakan.blastscale.economy.WalletSnapshot;

public record PurchaseResult(String item, int quantity, long coinsSpent, WalletSnapshot wallet) {
}
