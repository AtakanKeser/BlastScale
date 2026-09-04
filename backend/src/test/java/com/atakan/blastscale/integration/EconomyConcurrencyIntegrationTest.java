package com.atakan.blastscale.integration;

import org.junit.jupiter.api.Test;
import tools.jackson.databind.JsonNode;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;

import static org.assertj.core.api.Assertions.assertThat;

/** Concurrent debits can never overdraw a wallet: the row lock serialises them. */
class EconomyConcurrencyIntegrationTest extends AbstractIntegrationTest {

    @Test
    void concurrentPurchasesNeverOverdrawTheWallet() throws Exception {
        String token = api.register(uniqueUsername("shopper"));
        long coins = api.get("/api/v1/economy/wallet", token).number("coins"); // 500 by default
        int price = api.get("/api/v1/config", token).body().get("config").get("boosterPrices").get("HAMMER").asInt();
        long affordable = coins / price;

        int attempts = 60;
        ExecutorService pool = Executors.newFixedThreadPool(attempts);
        CountDownLatch go = new CountDownLatch(1);
        List<Future<ApiTestClient.Response>> futures = new ArrayList<>();
        for (int i = 0; i < attempts; i++) {
            futures.add(pool.submit(() -> {
                go.await(10, TimeUnit.SECONDS);
                return api.post("/api/v1/economy/shop/boosters", Map.of("boosterType", "HAMMER", "quantity", 1), token);
            }));
        }
        go.countDown();
        long ok = 0;
        long insufficient = 0;
        for (Future<ApiTestClient.Response> f : futures) {
            ApiTestClient.Response r = f.get(60, TimeUnit.SECONDS);
            if (r.status() == 200) {
                ok++;
            } else if ("INSUFFICIENT_COINS".equals(r.text("code"))) {
                insufficient++;
            } else {
                throw new AssertionError("unexpected response " + r.status() + " " + r.body());
            }
        }
        pool.shutdownNow();

        assertThat(ok).isEqualTo(affordable);
        assertThat(insufficient).isEqualTo(attempts - affordable);
        JsonNode wallet = api.get("/api/v1/economy/wallet", token).body();
        assertThat(wallet.get("coins").asLong()).isEqualTo(coins - affordable * price);
        assertThat(wallet.get("boosters").get("HAMMER").asInt()).isEqualTo((int) affordable);

        // ledger reconciles with the balance: initial grant + debits == current coins
        long sum = 0;
        for (JsonNode t : api.get("/api/v1/economy/transactions?size=100", token).body().get("content")) {
            if ("COIN".equals(t.get("resource").asText())) {
                sum += t.get("amount").asLong();
            }
        }
        assertThat(sum).isEqualTo(wallet.get("coins").asLong());
    }
}
