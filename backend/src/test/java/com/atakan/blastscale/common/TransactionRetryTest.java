package com.atakan.blastscale.common;

import org.junit.jupiter.api.Test;
import org.springframework.dao.CannotAcquireLockException;
import org.springframework.dao.DataIntegrityViolationException;

import java.util.concurrent.atomic.AtomicInteger;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

class TransactionRetryTest {

    @Test
    void retriesDeadlockVictimsAndSucceeds() {
        AtomicInteger attempts = new AtomicInteger();
        String result = TransactionRetry.run("test", () -> {
            if (attempts.incrementAndGet() < 3) {
                throw new CannotAcquireLockException("Deadlock found when trying to get lock");
            }
            return "ok";
        });
        assertThat(result).isEqualTo("ok");
        assertThat(attempts.get()).isEqualTo(3);
    }

    @Test
    void givesUpAfterThreeAttempts() {
        AtomicInteger attempts = new AtomicInteger();
        assertThatThrownBy(() -> TransactionRetry.run("test", () -> {
            attempts.incrementAndGet();
            throw new CannotAcquireLockException("Deadlock found when trying to get lock");
        })).isInstanceOf(CannotAcquireLockException.class);
        assertThat(attempts.get()).isEqualTo(3);
    }

    @Test
    void doesNotRetryOtherFailures() {
        AtomicInteger attempts = new AtomicInteger();
        assertThatThrownBy(() -> TransactionRetry.run("test", () -> {
            attempts.incrementAndGet();
            throw new DataIntegrityViolationException("duplicate key");
        })).isInstanceOf(DataIntegrityViolationException.class);
        assertThat(attempts.get()).isEqualTo(1);
    }
}
