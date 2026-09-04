package com.atakan.blastscale.player;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.common.redis.CacheProperties;
import com.atakan.blastscale.common.redis.RedisJsonCache;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Clock;
import java.time.Instant;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;

/**
 * Account lifecycle and the cached player profile.
 *
 * <p>Profile reads follow the cache-aside pattern: {@code player:{id}} in Redis with a 10 minute
 * TTL, MySQL as the source of truth. Any module that changes data shown in the profile evicts the
 * key (see {@link #evictProfile(long)}); the economy module does that through an application event
 * after every wallet change.
 */
@Service
public class PlayerService {

    static final String CACHE_NAME = "player_profile";

    private final PlayerRepository players;
    private final PasswordEncoder passwordEncoder;
    private final RedisJsonCache cache;
    private final CacheProperties cacheProperties;
    private final ApplicationEventPublisher events;
    private final Clock clock;
    private final Optional<PlayerProfileEnricher> profileEnricher;

    public PlayerService(PlayerRepository players, PasswordEncoder passwordEncoder, RedisJsonCache cache,
                         CacheProperties cacheProperties, ApplicationEventPublisher events, Clock clock,
                         Optional<PlayerProfileEnricher> profileEnricher) {
        this.players = players;
        this.passwordEncoder = passwordEncoder;
        this.cache = cache;
        this.cacheProperties = cacheProperties;
        this.events = events;
        this.clock = clock;
        this.profileEnricher = profileEnricher;
    }

    // ------------------------------------------------------------------ registration / login

    @Transactional
    public Player register(String username, String rawPassword) {
        return register(username, rawPassword, PlayerRole.PLAYER);
    }

    @Transactional
    public Player register(String username, String rawPassword, PlayerRole role) {
        if (players.existsByUsername(username)) {
            throw new BlastScaleException(ErrorCode.USERNAME_TAKEN, "Username '" + username + "' is already taken");
        }
        Player player = new Player(username, passwordEncoder.encode(rawPassword), null, role, Instant.now(clock));
        return persistNewPlayer(player, false);
    }

    /**
     * Guest login: the device id is the identity. Returns the existing player for a known device
     * or creates one with a generated username such as {@code guest_3f9a2c}.
     */
    @Transactional
    public Player loginOrCreateGuest(String deviceId) {
        Optional<Player> existing = players.findByDeviceId(deviceId);
        if (existing.isPresent()) {
            existing.get().touch(Instant.now(clock));
            return existing.get();
        }
        String username = "guest_" + UUID.randomUUID().toString().replace("-", "").substring(0, 8);
        Player player = new Player(username, null, deviceId, PlayerRole.PLAYER, Instant.now(clock));
        return persistNewPlayer(player, true);
    }

    @Transactional
    public Player authenticate(String username, String rawPassword) {
        Player player = players.findByUsername(username)
                .filter(p -> p.getPasswordHash() != null && passwordEncoder.matches(rawPassword, p.getPasswordHash()))
                .orElseThrow(() -> new BlastScaleException(ErrorCode.INVALID_CREDENTIALS, "Invalid username or password"));
        player.touch(Instant.now(clock));
        return player;
    }

    private Player persistNewPlayer(Player player, boolean guest) {
        try {
            Player saved = players.saveAndFlush(player);
            // Same transaction: listeners (wallet creation, telemetry) commit or roll back with us.
            events.publishEvent(new PlayerRegisteredEvent(saved.getId(), saved.getUsername(), guest));
            return saved;
        } catch (DataIntegrityViolationException e) {
            // Two concurrent registrations with the same username: the unique index wins the race.
            throw new BlastScaleException(ErrorCode.USERNAME_TAKEN, "Username '" + player.getUsername() + "' is already taken");
        }
    }

    // ------------------------------------------------------------------ profile

    /** Cache-aside read of the profile. */
    public PlayerProfile getProfile(long playerId) {
        return cache.getOrLoad(CACHE_NAME, cacheKey(playerId), PlayerProfile.class,
                cacheProperties.playerProfileTtl(), () -> loadProfile(playerId));
    }

    @Transactional(readOnly = true)
    public PlayerProfile loadProfile(long playerId) {
        Player player = requirePlayer(playerId);
        PlayerProfile.WalletSummary wallet = profileEnricher
                .map(enricher -> enricher.walletSummary(playerId))
                .orElse(null);
        return new PlayerProfile(player.getId(), player.getUsername(), player.getRole().name(),
                player.getCurrentLevel(), player.getCreatedAt(), wallet);
    }

    public void evictProfile(long playerId) {
        cache.evict(cacheKey(playerId));
    }

    @Transactional(readOnly = true)
    public Player requirePlayer(long playerId) {
        return players.findById(playerId)
                .orElseThrow(() -> new BlastScaleException(ErrorCode.PLAYER_NOT_FOUND, "Player " + playerId + " does not exist"));
    }

    @Transactional(readOnly = true)
    public Page<Player> search(String query, int page, int size) {
        return players.findByUsernameContainingIgnoreCaseOrderByIdDesc(query == null ? "" : query, PageRequest.of(page, size));
    }

    /**
     * Called by the progression module after a level is cleared. Returns the (possibly unchanged)
     * current level. Runs inside the caller's transaction.
     */
    @Transactional
    public int advanceLevel(long playerId, int completedLevel) {
        Player player = requirePlayer(playerId);
        if (player.advanceIfCurrent(completedLevel)) {
            evictProfile(playerId);
        }
        return player.getCurrentLevel();
    }

    /** Names for leaderboard rows; missing ids are simply absent from the map. */
    @Transactional(readOnly = true)
    public Map<Long, String> usernamesOf(Iterable<Long> ids) {
        return players.findAllById(ids).stream()
                .collect(java.util.stream.Collectors.toMap(Player::getId, Player::getUsername));
    }

    private static String cacheKey(long playerId) {
        return "player:" + playerId;
    }
}
