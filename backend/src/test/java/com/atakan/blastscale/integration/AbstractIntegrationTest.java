package com.atakan.blastscale.integration;

import org.junit.jupiter.api.BeforeEach;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.web.server.LocalServerPort;
import org.springframework.boot.testcontainers.service.connection.ServiceConnection;
import org.springframework.context.annotation.Import;
import org.testcontainers.containers.GenericContainer;
import org.testcontainers.elasticsearch.ElasticsearchContainer;
import org.testcontainers.lifecycle.Startables;
import org.testcontainers.mongodb.MongoDBContainer;
import org.testcontainers.mysql.MySQLContainer;
import org.testcontainers.utility.DockerImageName;
import tools.jackson.databind.ObjectMapper;

import java.time.Clock;
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

    protected ApiTestClient api;

    @BeforeEach
    void setUpClient() {
        api = new ApiTestClient(port, objectMapper);
        mutableClock().reset();
    }

    protected MutableClock mutableClock() {
        return (MutableClock) clock;
    }

    /** Unique usernames so tests never collide, even when re-run against a warm database. */
    protected static String uniqueUsername(String prefix) {
        return prefix + "_" + Long.toString(System.nanoTime() % 1_000_000_000L, 36) + PLAYER_SEQUENCE.incrementAndGet();
    }
}
