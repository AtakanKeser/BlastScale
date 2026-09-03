package com.atakan.blastscale.integration;

import org.junit.jupiter.api.BeforeEach;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.web.server.LocalServerPort;
import org.springframework.boot.testcontainers.service.connection.ServiceConnection;
import org.springframework.context.annotation.Import;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.testcontainers.containers.GenericContainer;
import org.testcontainers.elasticsearch.ElasticsearchContainer;
import org.testcontainers.lifecycle.Startables;
import org.testcontainers.mongodb.MongoDBContainer;
import org.testcontainers.mysql.MySQLContainer;
import org.testcontainers.utility.DockerImageName;
import tools.jackson.databind.ObjectMapper;

import java.time.Clock;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Boots the real application against real MySQL, Redis, MongoDB and Elasticsearch containers.
 *
 * <p>The containers are started once per JVM (static singleton pattern) and shared by every
 * integration test class; Spring's test context cache does the same for the application context,
 * so the ~40s container start-up is paid a single time for the whole suite.
 */
@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT, properties = {
        // validators/limits relaxed so tests are not throttled; each has its own dedicated test
        "blastscale.gameplay.min-millis-per-move=0",
        "blastscale.rate-limit.requests-per-minute=100000",
        "blastscale.outbox.poll-interval=200ms",
        "blastscale.cache.active-events-ttl=1s",
        // time-based jobs would interfere with clock manipulation; each job has its own test path
        "blastscale.jobs.enabled=false",
        "logging.level.com.atakan.blastscale=INFO"
})
@Import(TestClockConfig.class)
public abstract class AbstractIntegrationTest {

    @ServiceConnection
    static final MySQLContainer MYSQL = new MySQLContainer("mysql:8.4");

    @ServiceConnection(name = "redis")
    static final GenericContainer<?> REDIS = new GenericContainer<>("redis:7.4-alpine").withExposedPorts(6379);

    @ServiceConnection
    static final MongoDBContainer MONGO = new MongoDBContainer("mongo:8.0");

    @ServiceConnection
    static final ElasticsearchContainer ELASTICSEARCH = new ElasticsearchContainer(
            DockerImageName.parse("docker.elastic.co/elasticsearch/elasticsearch:9.1.5"))
            .withEnv("xpack.security.enabled", "false")
            .withEnv("ES_JAVA_OPTS", "-Xms256m -Xmx256m");

    static {
        Startables.deepStart(MYSQL, REDIS, MONGO, ELASTICSEARCH).join();
    }

    private static final AtomicInteger PLAYER_SEQUENCE = new AtomicInteger();

    @LocalServerPort
    protected int port;

    @Autowired
    protected ObjectMapper objectMapper;

    @Autowired
    protected Clock clock;

    @Autowired
    protected StringRedisTemplate redis;

    protected ApiTestClient api;

    @BeforeEach
    void setUpClient() {
        api = new ApiTestClient(port, objectMapper);
        mutableClock().reset();
        clearDerivedCaches();
    }

    /**
     * Drops every Redis entry that is only a cached projection of the database.
     *
     * <p>All test classes share one Redis container, and a class that manipulates the clock or runs
     * in a second Spring context can leave an entry behind whose TTL outlives it — which showed up
     * as live-event tests failing because a freshly created event was still missing from a cached
     * "active events" list. Derived caches can always be thrown away, so clearing them before each
     * test removes the coupling without hiding real behaviour. Authoritative Redis data (leaderboard
     * sorted sets, rate-limit counters, idempotency records) is deliberately left alone.
     */
    private void clearDerivedCaches() {
        Set<String> keys = new HashSet<>(List.of("events:active", "config:base", "experiments:live"));
        for (String pattern : List.of("player:*", "level:*", "experiments:player:*")) {
            Set<String> matches = redis.keys(pattern);
            if (matches != null) {
                keys.addAll(matches);
            }
        }
        redis.delete(keys);
    }

    protected MutableClock mutableClock() {
        return (MutableClock) clock;
    }

    /** Unique usernames so tests never collide, even when re-run against a warm database. */
    protected static String uniqueUsername(String prefix) {
        return prefix + "_" + Long.toString(System.nanoTime() % 1_000_000_000L, 36) + PLAYER_SEQUENCE.incrementAndGet();
    }
}
