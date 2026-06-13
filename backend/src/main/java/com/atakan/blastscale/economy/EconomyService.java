package com.atakan.blastscale.economy;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.common.metrics.GameplayMetrics;
import com.atakan.blastscale.economy.dto.TransactionView;
import com.atakan.blastscale.remoteconfig.ConfigKeys;
import com.atakan.blastscale.remoteconfig.RemoteConfigService;
import com.atakan.blastscale.remoteconfig.ResolvedConfig;
import com.atakan.blastscale.telemetry.TelemetryEventType;
import com.atakan.blastscale.telemetry.TelemetryService;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Clock;
import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Server-authoritative game economy.
 *
 * <p>The only way to change a balance is {@link #apply}, which runs the classic ledger recipe:
 * <pre>
 *   BEGIN
 *     SELECT wallet FOR UPDATE           -- serialise per player
 *     regenerate lives lazily
 *     reject duplicate (reason, reference) -- exactly-once rewards
 *     check balance >= debit               -- never negative
 *     INSERT economy_transaction           -- append-only ledger
 *     UPDATE wallet (version + 1)          -- optimistic guard on top
 *     INSERT outbox_event                  -- telemetry in the same transaction
 *   COMMIT
 * </pre>
 * Clients never send amounts; callers of this service compute them from server-side rules.
 */
@Service
public class EconomyService {

    private final WalletRepository wallets;
    private final PlayerBoosterRepository boosters;
    private final EconomyTransactionRepository transactions;
    private final RemoteConfigService config;
    private final TelemetryService telemetry;
    private final GameplayMetrics metrics;
    private final ApplicationEventPublisher events;
    private final Clock clock;

    public EconomyService(WalletRepository wallets, PlayerBoosterRepository boosters,
                          EconomyTransactionRepository transactions, RemoteConfigService config,
                          TelemetryService telemetry, GameplayMetrics metrics, ApplicationEventPublisher events,
                          Clock clock) {
        this.wallets = wallets;
        this.boosters = boosters;
        this.transactions = transactions;
        this.config = config;
        this.telemetry = telemetry;
        this.metrics = metrics;
        this.events = events;
        this.clock = clock;
    }

    // ------------------------------------------------------------------ wallet lifecycle

    /** Creates the wallet with the configured starting resources. Called on registration. */
    @Transactional
    public WalletSnapshot createWallet(long playerId) {
        ResolvedConfig cfg = config.resolveFor(playerId);
        long startingCoins = cfg.getInt(ConfigKeys.STARTING_COINS);
        int maxLives = cfg.maxLives();
        Instant now = Instant.now(clock);
        Wallet wallet = wallets.save(new Wallet(playerId, 0, 0, now));
        // Even the initial grant goes through the ledger, so "sum of ledger == balance" always holds.
        return applyLocked(wallet, cfg, List.of(
                        ResourceChange.credit(Resource.COIN, startingCoins),
                        ResourceChange.credit(Resource.LIFE, maxLives)),
                TransactionReason.INITIAL_GRANT, "player:" + playerId, now);
    }

    /** Current balances including lazily regenerated lives (read only, nothing is persisted). */
    @Transactional(readOnly = true)
    public WalletSnapshot getWallet(long playerId) {
        Wallet wallet = wallets.findById(playerId)
                .orElseThrow(() -> new BlastScaleException(ErrorCode.PLAYER_NOT_FOUND, "Wallet of player " + playerId + " does not exist"));
        return snapshot(wallet, config.resolveFor(playerId), Instant.now(clock));
    }

    // ------------------------------------------------------------------ balance changes

    @Transactional
    public WalletSnapshot credit(long playerId, Resource resource, long amount, TransactionReason reason, String referenceId) {
        return apply(playerId, List.of(ResourceChange.credit(resource, amount)), reason, referenceId);
    }

    @Transactional
    public WalletSnapshot debit(long playerId, Resource resource, long amount, TransactionReason reason, String referenceId) {
        return apply(playerId, List.of(ResourceChange.debit(resource, amount)), reason, referenceId);
    }

    /**
     * Applies a set of balance changes atomically.
     *
     * @throws BlastScaleException DUPLICATE_TRANSACTION when (reason, referenceId) was already applied,
     *                             INSUFFICIENT_* / NO_LIVES_LEFT when a debit cannot be covered
     */
    @Transactional
    public WalletSnapshot apply(long playerId, List<ResourceChange> changes, TransactionReason reason, String referenceId) {
        Wallet wallet = lock(playerId);
        return applyLocked(wallet, config.resolveFor(playerId), changes, reason, referenceId, Instant.now(clock));
    }

    /** Has this (reason, reference) already been paid to the player? Used by callers to short-circuit. */
    @Transactional(readOnly = true)
    public boolean wasApplied(long playerId, TransactionReason reason, String referenceId) {
        return transactions.existsByPlayerIdAndReasonAndReferenceId(playerId, reason, referenceId);
    }

    /** Consumes one life for a level attempt. */
    @Transactional
    public WalletSnapshot consumeLife(long playerId, String sessionId) {
        return apply(playerId, List.of(ResourceChange.debit(Resource.LIFE, 1)), TransactionReason.LEVEL_START, sessionId);
    }

    /** Refills lives to the maximum for the configured coin price. */
    @Transactional
    public WalletSnapshot refillLives(long playerId, String referenceId) {
        Wallet wallet = lock(playerId);
        ResolvedConfig cfg = config.resolveFor(playerId);
        Instant now = Instant.now(clock);
        regenerate(wallet, cfg, now);
        int missing = cfg.maxLives() - wallet.getLives();
        if (missing <= 0) {
            throw new BlastScaleException(ErrorCode.LIVES_ALREADY_FULL, "Lives are already full");
        }
        int price = cfg.getInt(ConfigKeys.LIFE_REFILL_PRICE);
        return applyLocked(wallet, cfg, List.of(
                        ResourceChange.debit(Resource.COIN, price),
                        ResourceChange.credit(Resource.LIFE, missing)),
                TransactionReason.BUY_LIVES, referenceId, now);
    }

    @Transactional(readOnly = true)
    public Page<TransactionView> transactions(long playerId, int page, int size) {
        return transactions.findByPlayerIdOrderByIdDesc(playerId, PageRequest.of(page, size)).map(TransactionView::from);
    }

    // ------------------------------------------------------------------ internals

    private Wallet lock(long playerId) {
        return wallets.lockByPlayerId(playerId)
                .orElseThrow(() -> new BlastScaleException(ErrorCode.PLAYER_NOT_FOUND, "Wallet of player " + playerId + " does not exist"));
    }

    private WalletSnapshot applyLocked(Wallet wallet, ResolvedConfig cfg, List<ResourceChange> changes,
                                       TransactionReason reason, String referenceId, Instant now) {
        long playerId = wallet.getPlayerId();
        if (transactions.existsByPlayerIdAndReasonAndReferenceId(playerId, reason, referenceId)) {
            throw new BlastScaleException(ErrorCode.DUPLICATE_TRANSACTION,
                    reason + " for reference " + referenceId + " was already applied",
                    Map.of("reason", reason.name(), "referenceId", referenceId));
        }
        regenerate(wallet, cfg, now);

        for (ResourceChange change : changes) {
            long balanceAfter = applyChange(wallet, cfg, change, now);
            transactions.save(new EconomyTransaction(playerId, change.resource(), change.amount(), balanceAfter,
                    reason, referenceId, now));
            metrics.economyTransaction(change.resource().name(), change.amount() >= 0 ? "CREDIT" : "DEBIT");
            telemetry.record(TelemetryEventType.ECONOMY_TRANSACTION, playerId, "wallet", referenceId, Map.of(
                    "resource", change.resource().name(),
                    "amount", change.amount(),
                    "balanceAfter", balanceAfter,
                    "reason", reason.name()));
        }
        events.publishEvent(new WalletChangedEvent(playerId));
        return snapshot(wallet, cfg, now);
    }

    /** Applies one change and returns the balance after it; throws when a debit is not covered. */
    private long applyChange(Wallet wallet, ResolvedConfig cfg, ResourceChange change, Instant now) {
        long amount = change.amount();
        switch (change.resource()) {
            case COIN -> {
                long after = wallet.getCoins() + amount;
                if (after < 0) {
                    throw new BlastScaleException(ErrorCode.INSUFFICIENT_COINS, "Not enough coins",
                            Map.of("required", -amount, "available", wallet.getCoins()));
                }
                wallet.setCoins(after);
                return after;
            }
            case LIFE -> {
                int after = (int) (wallet.getLives() + amount);
                if (after < 0) {
                    long nextIn = LifeRegeneration.apply(wallet.getLives(), wallet.getLivesUpdatedAt(), now,
                            cfg.maxLives(), cfg.lifeRegenerationMinutes()).nextLifeInSeconds();
                    throw new BlastScaleException(ErrorCode.NO_LIVES_LEFT,
                            "You have no lives left. Next life in " + nextIn + " seconds",
                            Map.of("nextLifeInSeconds", nextIn));
                }
                // Losing a life while full starts the regeneration timer from now.
                Instant reference = wallet.getLives() >= cfg.maxLives() && amount < 0 ? now : wallet.getLivesUpdatedAt();
                wallet.setLives(Math.min(after, Math.max(cfg.maxLives(), wallet.getLives())), reference);
                return wallet.getLives();
            }
            case STAR -> {
                int after = (int) (wallet.getStars() + amount);
                wallet.setStars(Math.max(0, after));
                return wallet.getStars();
            }
            default -> {
                BoosterType type = BoosterType.ofResource(change.resource());
                PlayerBooster booster = boosters.findByPlayerIdAndBoosterType(wallet.getPlayerId(), type)
                        .orElseGet(() -> boosters.save(new PlayerBooster(wallet.getPlayerId(), type)));
                int after = (int) (booster.getQuantity() + amount);
                if (after < 0) {
                    throw new BlastScaleException(ErrorCode.INSUFFICIENT_BOOSTERS, "Not enough " + type + " boosters",
                            Map.of("required", -amount, "available", booster.getQuantity()));
                }
                booster.setQuantity(after);
                return after;
            }
        }
    }

    private static void regenerate(Wallet wallet, ResolvedConfig cfg, Instant now) {
        LifeRegeneration.Result result = LifeRegeneration.apply(wallet.getLives(), wallet.getLivesUpdatedAt(), now,
                cfg.maxLives(), cfg.lifeRegenerationMinutes());
        wallet.setLives(result.lives(), result.livesUpdatedAt());
    }

    private WalletSnapshot snapshot(Wallet wallet, ResolvedConfig cfg, Instant now) {
        LifeRegeneration.Result regen = LifeRegeneration.apply(wallet.getLives(), wallet.getLivesUpdatedAt(), now,
                cfg.maxLives(), cfg.lifeRegenerationMinutes());
        Map<String, Integer> boosterCounts = new LinkedHashMap<>();
        for (BoosterType type : BoosterType.values()) {
            boosterCounts.put(type.name(), 0);
        }
        boosters.findByPlayerId(wallet.getPlayerId())
                .forEach(b -> boosterCounts.put(b.getBoosterType().name(), b.getQuantity()));
        return new WalletSnapshot(wallet.getCoins(), regen.lives(), cfg.maxLives(), regen.nextLifeInSeconds(),
                wallet.getStars(), boosterCounts);
    }
}
