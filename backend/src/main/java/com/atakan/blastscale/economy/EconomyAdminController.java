package com.atakan.blastscale.economy;

import com.atakan.blastscale.economy.dto.GrantRequest;
import com.atakan.blastscale.economy.dto.TransactionView;
import com.atakan.blastscale.security.CurrentPlayer;
import com.atakan.blastscale.security.PlayerPrincipal;
import com.atakan.blastscale.telemetry.TelemetryEventType;
import com.atakan.blastscale.telemetry.TelemetryService;
import jakarta.validation.Valid;
import org.springframework.data.domain.Page;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import java.util.Map;
import java.util.UUID;

/** Support tooling: inspect a player's ledger and compensate them. */
@RestController
@RequestMapping("/api/v1/admin/players/{playerId}")
@PreAuthorize("hasRole('ADMIN')")
public class EconomyAdminController {

    private final EconomyService economyService;
    private final TelemetryService telemetry;

    public EconomyAdminController(EconomyService economyService, TelemetryService telemetry) {
        this.economyService = economyService;
        this.telemetry = telemetry;
    }

    @GetMapping("/wallet")
    public WalletSnapshot wallet(@PathVariable long playerId) {
        return economyService.getWallet(playerId);
    }

    @GetMapping("/transactions")
    public Page<TransactionView> transactions(@PathVariable long playerId,
                                              @RequestParam(defaultValue = "0") int page,
                                              @RequestParam(defaultValue = "50") int size) {
        return economyService.transactions(playerId, page, Math.min(size, 200));
    }

    /** Manual compensation ("sorry for the outage, here are 500 coins"), fully audited in the ledger. */
    @PostMapping("/grant")
    public WalletSnapshot grant(@PathVariable long playerId, @Valid @RequestBody GrantRequest request,
                                @CurrentPlayer PlayerPrincipal admin) {
        String reference = "admin:" + UUID.randomUUID();
        WalletSnapshot wallet = economyService.apply(playerId,
                java.util.List.of(new ResourceChange(request.resource(), request.amount())),
                TransactionReason.ADMIN_GRANT, reference);
        telemetry.record(TelemetryEventType.ADMIN_GRANT, playerId, "wallet", reference, Map.of(
                "resource", request.resource().name(), "amount", request.amount(),
                "admin", admin.username(), "note", request.note() == null ? "" : request.note()));
        return wallet;
    }
}
