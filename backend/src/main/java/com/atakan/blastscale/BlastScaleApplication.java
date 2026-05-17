package com.atakan.blastscale;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.context.properties.ConfigurationPropertiesScan;
import org.springframework.scheduling.annotation.EnableScheduling;

/**
 * Entry point of the BlastScale backend.
 *
 * <p>The application is a <b>modular monolith</b>: each top-level package (player, economy,
 * progression, leaderboard, event, experiment, config, telemetry, ...) is a self-contained module
 * with its own controller/service/repository layers. Modules only talk to each other through their
 * service classes, which keeps the option open to extract any of them into a separate
 * deployable later without rewriting business logic.
 *
 * <ul>
 *   <li>{@link EnableScheduling} powers the background jobs (outbox publisher, leaderboard and
 *       live-event finalization). They are guarded by Redis locks so that running several API
 *       replicas does not execute a job twice.</li>
 *   <li>{@link ConfigurationPropertiesScan} binds the {@code blastscale.*} settings from
 *       application.yml to immutable {@code *Properties} records.</li>
 * </ul>
 */
@SpringBootApplication
@EnableScheduling
@ConfigurationPropertiesScan
public class BlastScaleApplication {

    public static void main(String[] args) {
        SpringApplication.run(BlastScaleApplication.class, args);
    }
}
