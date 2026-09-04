package com.atakan.blastscale.integration;

import com.atakan.blastscale.common.web.ApiHeaders;
import com.atakan.blastscale.economy.EconomyTransactionRepository;
import com.atakan.blastscale.economy.TransactionReason;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * The headline guarantee: no matter how many times a completion is sent — retries, duplicated
 * packets, a cheating client hammering the endpoint — the reward is paid <b>exactly once</b>.
 */
class ConcurrentRewardIntegrationTest extends AbstractIntegrationTest {

    private static final int THREADS = 100;

    @Autowired
    EconomyTransactionRepository transactions;

    @Test
    void hundredConcurrentCompletionsWithDifferentKeysPayOnce() throws Exception {
        String token = api.register(uniqueUsername("race"));
        long coinsBefore = api.get("/api/v1/economy/wallet", token).number("coins");
        ApiTestClient.Response start = api.startLevel(token, 1);
        Map<String, Object> body = api.solve(start.body());
        String sessionId = (String) body.get("sessionId");

        List<ApiTestClient.Response> responses = fire(() -> api.completeLevel(token, 1, body, UUID.randomUUID().toString()));

        long completed = responses.stream()
                .filter(r -> r.status() == 200 && "COMPLETED".equals(r.text("status"))
                        && "false".equals(r.headers().getFirst(ApiHeaders.IDEMPOTENT_REPLAYED)))
                .count();
        long alreadyProcessed = responses.stream().filter(r -> r.status() == 200 && "ALREADY_PROCESSED".equals(r.text("status"))).count();
        assertThat(completed).as("exactly one request wins the session").isEqualTo(1);
        assertThat(alreadyProcessed).isEqualTo(THREADS - 1);

        long reward = responses.stream().filter(r -> "COMPLETED".equals(r.text("status"))).findFirst()
                .orElseThrow().body().get("reward").get("coins").asLong();
        assertThat(api.get("/api/v1/economy/wallet", token).number("coins")).isEqualTo(coinsBefore + reward);
        assertThat(transactions.countByReasonAndReferenceId(TransactionReason.LEVEL_COMPLETE, sessionId))
                .as("ledger rows for the session (coins + stars)").isBetween(1L, 2L);
        assertThat(transactions.findByPlayerIdAndReasonAndReferenceId(api.playerId(token), TransactionReason.LEVEL_COMPLETE, sessionId)
                .stream().filter(t -> t.getResource().name().equals("COIN")).count()).isEqualTo(1);
    }

    @Test
    void hundredConcurrentRetriesWithTheSameKeyExecuteOnce() throws Exception {
        String token = api.register(uniqueUsername("retry"));
        long coinsBefore = api.get("/api/v1/economy/wallet", token).number("coins");
        ApiTestClient.Response start = api.startLevel(token, 1);
        Map<String, Object> body = api.solve(start.body());
        String key = UUID.randomUUID().toString();

        List<ApiTestClient.Response> responses = fire(() -> api.completeLevel(token, 1, body, key));

        long executed = responses.stream()
                .filter(r -> r.status() == 200 && "false".equals(r.headers().getFirst(ApiHeaders.IDEMPOTENT_REPLAYED))
                        && "COMPLETED".equals(r.text("status")))
                .count();
        long replayed = responses.stream().filter(r -> "true".equals(r.headers().getFirst(ApiHeaders.IDEMPOTENT_REPLAYED))).count();
        long inProgress = responses.stream().filter(r -> r.status() == 409 && "IDEMPOTENT_REQUEST_IN_PROGRESS".equals(r.text("code"))).count();
        assertThat(executed).isEqualTo(1);
        assertThat(executed + replayed + inProgress).isEqualTo(THREADS);

        long reward = responses.stream().filter(r -> r.status() == 200).findFirst().orElseThrow().body().get("reward").get("coins").asLong();
        assertThat(api.get("/api/v1/economy/wallet", token).number("coins")).isEqualTo(coinsBefore + reward);
    }

    /** Runs the call on {@value THREADS} threads released simultaneously by a latch. */
    private List<ApiTestClient.Response> fire(java.util.function.Supplier<ApiTestClient.Response> call) throws Exception {
        ExecutorService pool = Executors.newFixedThreadPool(THREADS);
        CountDownLatch ready = new CountDownLatch(THREADS);
        CountDownLatch go = new CountDownLatch(1);
        List<Future<ApiTestClient.Response>> futures = new ArrayList<>();
        for (int i = 0; i < THREADS; i++) {
            futures.add(pool.submit(() -> {
                ready.countDown();
                go.await(10, TimeUnit.SECONDS);
                return call.get();
            }));
        }
        ready.await(10, TimeUnit.SECONDS);
        go.countDown();
        List<ApiTestClient.Response> responses = new ArrayList<>();
        for (Future<ApiTestClient.Response> f : futures) {
            responses.add(f.get(60, TimeUnit.SECONDS));
        }
        pool.shutdownNow();
        return responses;
    }
}
