package com.atakan.blastscale.economy;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.economy.dto.PurchaseResult;
import com.atakan.blastscale.remoteconfig.ConfigKeys;
import com.atakan.blastscale.remoteconfig.RemoteConfigService;
import com.atakan.blastscale.telemetry.TelemetryEventType;
import com.atakan.blastscale.telemetry.TelemetryService;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;
import java.util.Map;
import java.util.UUID;

/** In-game shop: prices come from remote config, payment is a ledger debit. */
@Service
public class ShopService {

    private final EconomyService economyService;
    private final RemoteConfigService config;
    private final TelemetryService telemetry;

    public ShopService(EconomyService economyService, RemoteConfigService config, TelemetryService telemetry) {
        this.economyService = economyService;
        this.config = config;
        this.telemetry = telemetry;
    }

    @Transactional
    public PurchaseResult buyBooster(long playerId, BoosterType type, int quantity, String referenceId) {
        Map<String, Integer> prices = config.resolveFor(playerId).getIntMap(ConfigKeys.BOOSTER_PRICES);
        Integer unitPrice = prices.get(type.name());
        if (unitPrice == null) {
            throw new BlastScaleException(ErrorCode.NOT_FOUND, "Booster " + type + " is not for sale");
        }
        long total = (long) unitPrice * quantity;
        String reference = referenceId != null ? referenceId : UUID.randomUUID().toString();
        WalletSnapshot wallet = economyService.apply(playerId, List.of(
                        ResourceChange.debit(Resource.COIN, total),
                        ResourceChange.credit(type.resource(), quantity)),
                TransactionReason.BUY_BOOSTER, reference);
        telemetry.record(TelemetryEventType.BOOSTER_PURCHASED, playerId, "shop", reference,
                Map.of("booster", type.name(), "quantity", quantity, "coins", total));
        return new PurchaseResult(type.name(), quantity, total, wallet);
    }

    @Transactional
    public PurchaseResult buyLives(long playerId, String referenceId) {
        String reference = referenceId != null ? referenceId : UUID.randomUUID().toString();
        WalletSnapshot before = economyService.getWallet(playerId);
        WalletSnapshot wallet = economyService.refillLives(playerId, reference);
        long spent = before.coins() - wallet.coins();
        telemetry.record(TelemetryEventType.LIVES_PURCHASED, playerId, "shop", reference,
                Map.of("livesAfter", wallet.lives(), "coins", spent));
        return new PurchaseResult("LIVES", wallet.lives() - before.lives(), spent, wallet);
    }
}
