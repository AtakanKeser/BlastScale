package com.atakan.blastscale.integration;

import com.atakan.blastscale.common.web.ApiHeaders;
import com.atakan.blastscale.level.engine.BoardConfig;
import com.atakan.blastscale.level.engine.BoardEngine;
import com.atakan.blastscale.level.engine.GreedySolver;
import com.atakan.blastscale.level.engine.Move;
import com.atakan.blastscale.level.engine.SimulationResult;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.http.client.JdkClientHttpRequestFactory;
import org.springframework.web.client.RestClient;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Thin HTTP helper for integration tests: raw status codes + parsed JSON, no exceptions on 4xx/5xx. */
public class ApiTestClient {

    private final RestClient client;
    private final ObjectMapper mapper;

    public ApiTestClient(int port, ObjectMapper mapper) {
        // JDK HttpClient: unlike Apache HttpClient it does not silently retry a 429 after Retry-After,
        // which would turn rate-limit assertions into 60 second waits.
        this.client = RestClient.builder()
                .baseUrl("http://localhost:" + port)
                .requestFactory(new JdkClientHttpRequestFactory())
                .build();
        this.mapper = mapper;
    }

    public record Response(int status, JsonNode body, HttpHeaders headers) {
        public String text(String field) {
            return body.path(field).asText();
        }

        public int integer(String field) {
            return body.path(field).asInt();
        }

        public long number(String field) {
            return body.path(field).asLong();
        }

        public boolean is2xx() {
            return status >= 200 && status < 300;
        }
    }

    public Response get(String path, String token) {
        return client.get().uri(path).headers(h -> auth(h, token, null)).exchange(this::toResponse);
    }

    public Response post(String path, Object body, String token) {
        return post(path, body, token, null);
    }

    public Response post(String path, Object body, String token, String idempotencyKey) {
        RestClient.RequestBodySpec spec = client.post().uri(path).headers(h -> auth(h, token, idempotencyKey));
        if (body != null) {
            spec = spec.contentType(MediaType.APPLICATION_JSON).body(mapper.writeValueAsString(body));
        }
        return spec.exchange(this::toResponse);
    }

    public Response put(String path, Object body, String token) {
        return client.put().uri(path).headers(h -> auth(h, token, null))
                .contentType(MediaType.APPLICATION_JSON).body(mapper.writeValueAsString(body))
                .exchange(this::toResponse);
    }

    // ------------------------------------------------------------------ higher level helpers

    public String register(String username) {
        Response r = post("/api/v1/auth/register", Map.of("username", username, "password", "password123"), null);
        if (r.status() != 201) {
            throw new IllegalStateException("register failed: " + r.body());
        }
        return r.text("token");
    }

    public long playerId(String token) {
        return get("/api/v1/players/me", token).number("id");
    }

    public String adminToken() {
        Response r = post("/api/v1/auth/login", Map.of("username", "admin", "password", "admin12345"), null);
        if (r.status() != 200) {
            throw new IllegalStateException("admin login failed: " + r.body());
        }
        return r.text("token");
    }

    public Response startLevel(String token, int level) {
        return post("/api/v1/levels/" + level + "/start", null, token);
    }

    /** Builds a winning completion body for a start response using the greedy bot. */
    public Map<String, Object> solve(JsonNode start) {
        JsonNode b = start.get("board");
        List<Integer> thresholds = new ArrayList<>();
        b.get("starThresholds").forEach(n -> thresholds.add(n.asInt()));
        BoardConfig config = new BoardConfig(b.get("rows").asInt(), b.get("cols").asInt(), b.get("colorCount").asInt(),
                b.get("moveLimit").asInt(), b.get("targetScore").asInt(), thresholds);
        int seed = start.get("seed").asInt();
        List<Move> moves = GreedySolver.solve(config, seed);
        SimulationResult result = BoardEngine.simulate(config, seed, moves, false);
        if (!result.objectiveReached()) {
            throw new IllegalStateException("greedy solver could not clear the level for seed " + seed);
        }
        List<Map<String, Object>> moveList = new ArrayList<>();
        for (Move m : moves) {
            moveList.add(Map.of("type", m.type().name(), "row", m.row(), "col", m.col()));
        }
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("sessionId", start.get("sessionId").asText());
        body.put("score", result.score());
        body.put("movesUsed", result.movesUsed());
        body.put("moves", moveList);
        body.put("extraMovesUsed", false);
        return body;
    }

    public Response completeLevel(String token, int level, Map<String, Object> body, String idempotencyKey) {
        return post("/api/v1/levels/" + level + "/complete", body, token, idempotencyKey);
    }

    /** start + solve + complete in one go; returns the completion response. */
    public Response playAndWin(String token, int level) {
        Response start = startLevel(token, level);
        if (!start.is2xx()) {
            throw new IllegalStateException("start failed: " + start.body());
        }
        return completeLevel(token, level, solve(start.body()), java.util.UUID.randomUUID().toString());
    }

    // ------------------------------------------------------------------ internals

    private static void auth(HttpHeaders headers, String token, String idempotencyKey) {
        if (token != null) {
            headers.setBearerAuth(token);
        }
        if (idempotencyKey != null) {
            headers.set(ApiHeaders.IDEMPOTENCY_KEY, idempotencyKey);
        }
    }

    private Response toResponse(org.springframework.http.HttpRequest request,
                                RestClient.RequestHeadersSpec.ConvertibleClientHttpResponse response) {
        try {
            byte[] bytes = response.getBody().readAllBytes();
            JsonNode body = bytes.length == 0 ? mapper.nullNode() : mapper.readTree(bytes);
            return new Response(response.getStatusCode().value(), body, response.getHeaders());
        } catch (IOException e) {
            throw new UncheckedIOException(e);
        }
    }
}
