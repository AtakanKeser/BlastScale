package com.atakan.blastscale.economy;

import com.atakan.blastscale.player.PlayerProfile;
import com.atakan.blastscale.player.PlayerProfileEnricher;
import org.springframework.stereotype.Component;

/** Contributes wallet balances to the cached player profile. */
@Component
public class EconomyProfileEnricher implements PlayerProfileEnricher {

    private final EconomyService economyService;

    public EconomyProfileEnricher(EconomyService economyService) {
        this.economyService = economyService;
    }

    @Override
    public PlayerProfile.WalletSummary walletSummary(long playerId) {
        WalletSnapshot w = economyService.getWallet(playerId);
        return new PlayerProfile.WalletSummary(w.coins(), w.lives(), w.maxLives(), w.nextLifeInSeconds(), w.stars(), w.boosters());
    }
}
