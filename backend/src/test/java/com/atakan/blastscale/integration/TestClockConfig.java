package com.atakan.blastscale.integration;

import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Primary;

import java.time.Clock;

/** Replaces the application's system clock with a {@link MutableClock} in integration tests. */
@TestConfiguration
public class TestClockConfig {

    @Bean
    @Primary
    public Clock testClock() {
        return new MutableClock();
    }
}
