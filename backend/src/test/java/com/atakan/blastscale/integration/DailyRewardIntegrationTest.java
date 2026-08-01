package com.atakan.blastscale.integration;

import com.atakan.blastscale.common.web.ApiHeaders;
import org.junit.jupiter.api.Test;

import java.time.Duration;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;

class DailyRewardIntegrationTest extends AbstractIntegrationTest {

    @Test
    void claimOncePerDayWithStreaks() {
        String token = api.register(uniqueUsername("daily"));
        long before = api.get("/api/v1/economy/wallet", token).number("coins");
        assertThat(api.get("/api/v1/economy/daily-reward", token).body().get("available").asBoolean()).isTrue();

        String key = UUID.randomUUID().toString();
        ApiTestClient.Response day1 = api.post("/api/v1/economy/daily-reward", null, token, key);
        assertThat(day1.status()).isEqualTo(200);
        assertThat(day1.integer("streak")).isEqualTo(1);
        int coins = day1.integer("coins");
        assertThat(api.get("/api/v1/economy/wallet", token).number("coins")).isEqualTo(before + coins);

        ApiTestClient.Response replay = api.post("/api/v1/economy/daily-reward", null, token, key);
        assertThat(replay.status()).isEqualTo(200);
        assertThat(replay.headers().getFirst(ApiHeaders.IDEMPOTENT_REPLAYED)).isEqualTo("true");

        ApiTestClient.Response duplicate = api.post("/api/v1/economy/daily-reward", null, token, UUID.randomUUID().toString());
        assertThat(duplicate.status()).isEqualTo(409);
        assertThat(duplicate.text("code")).isEqualTo("DAILY_REWARD_ALREADY_CLAIMED");
        assertThat(api.get("/api/v1/economy/wallet", token).number("coins")).isEqualTo(before + coins);

        mutableClock().advance(Duration.ofDays(1));
        ApiTestClient.Response day2 = api.post("/api/v1/economy/daily-reward", null, token, UUID.randomUUID().toString());
        assertThat(day2.status()).isEqualTo(200);
        assertThat(day2.integer("streak")).isEqualTo(2);
        assertThat(day2.integer("coins")).isGreaterThan(coins); // streak bonus

        mutableClock().advance(Duration.ofDays(2)); // skipped a day: streak resets
        ApiTestClient.Response day4 = api.post("/api/v1/economy/daily-reward", null, token, UUID.randomUUID().toString());
        assertThat(day4.integer("streak")).isEqualTo(1);
    }
}
