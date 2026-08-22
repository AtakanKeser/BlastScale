package com.atakan.blastscale.common;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.PessimisticLockingFailureException;

import java.util.concurrent.ThreadLocalRandom;
import java.util.function.Supplier;

/**
 * Re-runs a transactional unit of work when MySQL reports a deadlock or lock wait timeout.
 *
 * <p>InnoDB resolves deadlocks by rolling back one victim and telling it to "try restarting
 * transaction"; that is exactly what this does, a bounded number of times with a small random
 * backoff. It must wrap the <em>whole</em> transaction (the call to the {@code @Transactional}
 * method through its proxy), never a statement inside it, because the victim's transaction is
 * already rolled back when the exception surfaces.
 */
public final class TransactionRetry {

    private static final Logger log = LoggerFactory.getLogger(TransactionRetry.class);
    private static final int MAX_ATTEMPTS = 3;

    private TransactionRetry() {
    }

    public static <T> T run(String operation, Supplier<T> work) {
        for (int attempt = 1; ; attempt++) {
            try {
                return work.get();
            } catch (PessimisticLockingFailureException e) {
                // Parent of CannotAcquireLockException (deadlock) and DeadlockLoserDataAccessException.
                if (attempt >= MAX_ATTEMPTS) {
                    throw e;
                }
                long backoffMillis = 20L * attempt + ThreadLocalRandom.current().nextInt(30);
                log.warn("{}: lock conflict on attempt {}/{} ({}), retrying in {} ms", operation, attempt, MAX_ATTEMPTS,
                        e.getClass().getSimpleName(), backoffMillis);
                try {
                    Thread.sleep(backoffMillis);
                } catch (InterruptedException interrupted) {
                    Thread.currentThread().interrupt();
                    throw e;
                }
            }
        }
    }
}
