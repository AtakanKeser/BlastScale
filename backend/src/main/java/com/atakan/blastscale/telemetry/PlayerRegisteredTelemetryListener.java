package com.atakan.blastscale.telemetry;

import com.atakan.blastscale.player.PlayerRegisteredEvent;
import org.springframework.context.event.EventListener;
import org.springframework.stereotype.Component;

import java.util.Map;

/** Records account creation. Runs inside the registration transaction (synchronous listener). */
@Component
public class PlayerRegisteredTelemetryListener {

    private final TelemetryService telemetry;

    public PlayerRegisteredTelemetryListener(TelemetryService telemetry) {
        this.telemetry = telemetry;
    }

    @EventListener
    public void on(PlayerRegisteredEvent event) {
        telemetry.record(TelemetryEventType.PLAYER_REGISTERED, event.playerId(), "player",
                Long.toString(event.playerId()), Map.of("username", event.username(), "guest", event.guest()));
    }
}
