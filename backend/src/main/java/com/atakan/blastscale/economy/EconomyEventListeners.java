package com.atakan.blastscale.economy;

import com.atakan.blastscale.player.PlayerRegisteredEvent;
import com.atakan.blastscale.player.PlayerService;
import org.springframework.context.event.EventListener;
import org.springframework.stereotype.Component;
import org.springframework.transaction.event.TransactionPhase;
import org.springframework.transaction.event.TransactionalEventListener;

/** Reactions of the economy module to events published by other modules or by itself. */
@Component
public class EconomyEventListeners {

    private final EconomyService economyService;
    private final PlayerService playerService;

    public EconomyEventListeners(EconomyService economyService, PlayerService playerService) {
        this.economyService = economyService;
        this.playerService = playerService;
    }

    /** New account -> wallet with starting resources, inside the registration transaction. */
    @EventListener
    public void onPlayerRegistered(PlayerRegisteredEvent event) {
        economyService.createWallet(event.playerId());
    }

    /**
     * Wallet committed -> drop the cached profile so the next read sees fresh balances.
     * AFTER_COMMIT guarantees we never evict before the new state is visible to other transactions.
     */
    @TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
    public void onWalletChanged(WalletChangedEvent event) {
        playerService.evictProfile(event.playerId());
    }
}
