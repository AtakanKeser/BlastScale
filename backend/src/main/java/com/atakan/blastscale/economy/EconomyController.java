package com.atakan.blastscale.economy;

import com.atakan.blastscale.common.idempotency.IdempotencyService;
import com.atakan.blastscale.common.idempotency.IdempotentResponses;
import com.atakan.blastscale.common.web.ApiHeaders;
import com.atakan.blastscale.economy.dto.DailyRewardResult;
import com.atakan.blastscale.economy.dto.DailyRewardStatus;
import com.atakan.blastscale.economy.dto.PurchaseBoosterRequest;
import com.atakan.blastscale.economy.dto.PurchaseResult;
import com.atakan.blastscale.economy.dto.TransactionView;
import com.atakan.blastscale.security.CurrentPlayer;
import com.atakan.blastscale.security.PlayerPrincipal;
import jakarta.validation.Valid;
import org.springframework.data.domain.Page;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

/**
 * Player-facing economy endpoints. Every mutating call accepts an {@code Idempotency-Key} header;
 * a retried request with the same key returns the original response instead of paying twice.
 */
@RestController
@RequestMapping("/api/v1/economy")
public class EconomyController {

    private final EconomyService economyService;
    private final DailyRewardService dailyRewardService;
    private final ShopService shopService;
    private final IdempotencyService idempotency;

    public EconomyController(EconomyService economyService, DailyRewardService dailyRewardService,
                             ShopService shopService, IdempotencyService idempotency) {
        this.economyService = economyService;
        this.dailyRewardService = dailyRewardService;
        this.shopService = shopService;
        this.idempotency = idempotency;
    }

    @GetMapping("/wallet")
    public WalletSnapshot wallet(@CurrentPlayer PlayerPrincipal principal) {
        return economyService.getWallet(principal.playerId());
    }

    @GetMapping("/transactions")
    public Page<TransactionView> transactions(@CurrentPlayer PlayerPrincipal principal,
                                              @RequestParam(defaultValue = "0") int page,
                                              @RequestParam(defaultValue = "20") int size) {
        return economyService.transactions(principal.playerId(), page, Math.min(size, 100));
    }

    @GetMapping("/daily-reward")
    public DailyRewardStatus dailyRewardStatus(@CurrentPlayer PlayerPrincipal principal) {
        return dailyRewardService.status(principal.playerId());
    }

    @PostMapping("/daily-reward")
    public ResponseEntity<DailyRewardResult> claimDailyReward(
            @CurrentPlayer PlayerPrincipal principal,
            @RequestHeader(value = ApiHeaders.IDEMPOTENCY_KEY, required = false) String idempotencyKey) {
        long playerId = principal.playerId();
        return IdempotentResponses.of(idempotency.execute("daily-reward", playerId, idempotencyKey,
                DailyRewardResult.class, () -> dailyRewardService.claim(playerId)));
    }

    @PostMapping("/shop/boosters")
    public ResponseEntity<PurchaseResult> buyBooster(
            @CurrentPlayer PlayerPrincipal principal,
            @Valid @RequestBody PurchaseBoosterRequest request,
            @RequestHeader(value = ApiHeaders.IDEMPOTENCY_KEY, required = false) String idempotencyKey) {
        long playerId = principal.playerId();
        return IdempotentResponses.of(idempotency.execute("buy-booster", playerId, idempotencyKey,
                PurchaseResult.class, () -> shopService.buyBooster(playerId, request.boosterType(), request.quantity(), idempotencyKey)));
    }

    @PostMapping("/shop/lives")
    public ResponseEntity<PurchaseResult> buyLives(
            @CurrentPlayer PlayerPrincipal principal,
            @RequestHeader(value = ApiHeaders.IDEMPOTENCY_KEY, required = false) String idempotencyKey) {
        long playerId = principal.playerId();
        return IdempotentResponses.of(idempotency.execute("buy-lives", playerId, idempotencyKey,
                PurchaseResult.class, () -> shopService.buyLives(playerId, idempotencyKey)));
    }
}
