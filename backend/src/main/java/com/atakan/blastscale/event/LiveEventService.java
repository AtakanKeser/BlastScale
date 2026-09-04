package com.atakan.blastscale.event;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.common.redis.CacheProperties;
import com.atakan.blastscale.common.redis.RedisJsonCache;
import com.atakan.blastscale.economy.EconomyService;
import com.atakan.blastscale.economy.Resource;
import com.atakan.blastscale.economy.TransactionReason;
import com.atakan.blastscale.event.dto.CreateEventRequest;
import com.atakan.blastscale.event.dto.EventPointsAwarded;
import com.atakan.blastscale.event.dto.LiveEventView;
import com.atakan.blastscale.event.dto.PlayerEventView;
import com.atakan.blastscale.player.PlayerService;
import com.atakan.blastscale.remoteconfig.ConfigKeys;
import com.atakan.blastscale.remoteconfig.RemoteConfigService;
import com.atakan.blastscale.telemetry.TelemetryEventType;
import com.atakan.blastscale.telemetry.TelemetryService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.domain.PageRequest;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import tools.jackson.core.type.TypeReference;
import tools.jackson.databind.ObjectMapper;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Optional;

/**
 * Live events ("LiveOps"): time-boxed rule changes started from the admin panel with zero
 * client or server deploys.
 *
 * <p>The active-event list is read on every level completion and every config fetch, so it is
 * cached in Redis for 30 seconds. Participation and ranking use MySQL (transactional with the
 * completion) — a Redis sorted set is the right tool for the always-on weekly leaderboard, while a
 * 48h event with a prize table needs durable, exactly-once bookkeeping more than sub-millisecond
 * rank reads.
 */
@Service
public class LiveEventService {

    private static final Logger log = LoggerFactory.getLogger(LiveEventService.class);
    private static final String ACTIVE_KEY = "events:active";
    private static final TypeReference<Map<String, Object>> MAP = new TypeReference<>() {
    };
    private static final int TOP_SIZE = 10;

    private final LiveEventRepository events;
    private final EventParticipationRepository participations;
    private final EventRuleParser ruleParser;
    private final EconomyService economyService;
    private final PlayerService playerService;
    private final RemoteConfigService config;
    private final RedisJsonCache cache;
    private final CacheProperties cacheProperties;
    private final TelemetryService telemetry;
    private final ObjectMapper objectMapper;
    private final Clock clock;

    public LiveEventService(LiveEventRepository events, EventParticipationRepository participations,
                            EventRuleParser ruleParser, EconomyService economyService, PlayerService playerService,
                            RemoteConfigService config, RedisJsonCache cache, CacheProperties cacheProperties,
                            TelemetryService telemetry, ObjectMapper objectMapper, Clock clock) {
        this.events = events;
        this.participations = participations;
        this.ruleParser = ruleParser;
        this.economyService = economyService;
        this.playerService = playerService;
        this.config = config;
        this.cache = cache;
        this.cacheProperties = cacheProperties;
        this.telemetry = telemetry;
        this.objectMapper = objectMapper;
        this.clock = clock;
    }

    // ------------------------------------------------------------------ gameplay hooks

    /** Active events (status ACTIVE and inside their window), cached for 30s. */
    public List<LiveEventView> activeEvents() {
        Instant now = Instant.now(clock);
        List<LiveEventView> cached = cache.getOrLoad("active_events", ACTIVE_KEY, ActiveEvents.class,
                cacheProperties.activeEventsTtl(), () -> new ActiveEvents(
                        events.findByStatusIn(List.of(LiveEventStatus.ACTIVE)).stream()
                                .filter(e -> e.isActive(now))
                                .map(e -> toView(e, false))
                                .toList())).events();
        // The cache may be up to 30s stale; re-check the window so an event never runs past endAt.
        return cached.stream().filter(e -> !now.isBefore(e.startAt()) && now.isBefore(e.endAt())).toList();
    }

    /** Multiplier of the strongest active DOUBLE_REWARD event, or 1.0. */
    public double doubleRewardMultiplier() {
        double multiplier = 1.0;
        for (LiveEventView event : activeEvents()) {
            if (LiveEventType.DOUBLE_REWARD.name().equals(event.type())) {
                EventRule.DoubleRewardRule rule = (EventRule.DoubleRewardRule) rule(event);
                multiplier = Math.max(multiplier, rule.multiplier());
            }
        }
        return multiplier;
    }

    /**
     * Called inside the level-completion transaction: awards Rocket Race points to every active
     * race the player is eligible for.
     */
    @Transactional
    public List<EventPointsAwarded> recordLevelCompletion(long playerId, int playerLevel, int completedLevel) {
        if (!config.baseConfigBoolean(ConfigKeys.ROCKET_RACE_ENABLED)) {
            return List.of();
        }
        List<EventPointsAwarded> awarded = new ArrayList<>();
        Instant now = Instant.now(clock);
        for (LiveEventView event : activeEvents()) {
            if (!LiveEventType.ROCKET_RACE.name().equals(event.type())) {
                continue;
            }
            EventRule.RocketRaceRule rule = (EventRule.RocketRaceRule) rule(event);
            if (completedLevel < rule.minimumLevel()) {
                continue;
            }
            participations.addPoints(event.id(), playerId, rule.pointsPerLevel(), now);
            long total = participations.findById(new EventParticipation.Id(event.id(), playerId))
                    .map(EventParticipation::getPoints).orElse((long) rule.pointsPerLevel());
            awarded.add(new EventPointsAwarded(event.id(), event.name(), event.type(), rule.pointsPerLevel(), total));
        }
        return awarded;
    }

    // ------------------------------------------------------------------ player screen

    @Transactional(readOnly = true)
    public List<PlayerEventView> eventsFor(long playerId, int playerLevel) {
        Instant now = Instant.now(clock);
        List<PlayerEventView> result = new ArrayList<>();
        for (LiveEventView event : activeEvents()) {
            Optional<EventParticipation> mine = participations.findById(new EventParticipation.Id(event.id(), playerId));
            long myPoints = mine.map(EventParticipation::getPoints).orElse(0L);
            Integer myRank = mine.map(p -> (int) participations.rankOf(event.id(), p.getPoints(), p.getUpdatedAt())).orElse(null);
            boolean eligible = true;
            if (LiveEventType.ROCKET_RACE.name().equals(event.type())) {
                eligible = playerLevel >= ((EventRule.RocketRaceRule) rule(event)).minimumLevel();
            }
            result.add(new PlayerEventView(event.id(), event.type(), event.name(), event.startAt(), event.endAt(),
                    Math.max(0, Duration.between(now, event.endAt()).getSeconds()), event.configuration(),
                    myPoints, myRank, eligible, standings(event.id(), TOP_SIZE)));
        }
        return result;
    }

    // ------------------------------------------------------------------ admin

    @Transactional(readOnly = true)
    public List<LiveEventView> listAll() {
        return events.findAllByOrderByIdDesc().stream().map(e -> toView(e, true)).toList();
    }

    @Transactional(readOnly = true)
    public LiveEventView get(long id) {
        return toView(require(id), true);
    }

    @Transactional
    public LiveEventView create(CreateEventRequest request) {
        Instant now = Instant.now(clock);
        Instant startAt = request.startAt() == null ? now : request.startAt();
        if (!request.endAt().isAfter(startAt)) {
            throw new BlastScaleException(ErrorCode.VALIDATION_ERROR, "endAt must be after startAt");
        }
        String json = objectMapper.writeValueAsString(request.configuration() == null ? Map.of() : request.configuration());
        ruleParser.parse(request.type(), json); // fail fast on a bad configuration
        LiveEventStatus status = startAt.isAfter(now) ? LiveEventStatus.SCHEDULED : LiveEventStatus.ACTIVE;
        LiveEvent event = events.save(new LiveEvent(request.type(), request.name(), startAt, request.endAt(), json, status, now));
        cache.evict(ACTIVE_KEY);
        return toView(event, true);
    }

    /** Starts a SCHEDULED event right now. */
    @Transactional
    public LiveEventView activate(long id) {
        LiveEvent event = require(id);
        if (event.getStatus() != LiveEventStatus.SCHEDULED) {
            throw new BlastScaleException(ErrorCode.EVENT_INVALID_STATE, "Only SCHEDULED events can be activated");
        }
        Instant now = Instant.now(clock);
        event.setWindow(now, event.getEndAt().isAfter(now) ? event.getEndAt() : now.plus(Duration.ofHours(48)), now);
        event.transition(LiveEventStatus.ACTIVE, now);
        cache.evict(ACTIVE_KEY);
        return toView(event, true);
    }

    /** Ends an ACTIVE event now and pays the prizes. */
    @Transactional
    public LiveEventView end(long id) {
        LiveEvent event = require(id);
        if (event.getStatus() != LiveEventStatus.ACTIVE) {
            throw new BlastScaleException(ErrorCode.EVENT_INVALID_STATE, "Only ACTIVE events can be ended");
        }
        Instant now = Instant.now(clock);
        event.setWindow(event.getStartAt(), now, now);
        event.transition(LiveEventStatus.ENDED, now);
        cache.evict(ACTIVE_KEY);
        finalizeEvent(event);
        return toView(event, true);
    }

    @Transactional
    public LiveEventView cancel(long id) {
        LiveEvent event = require(id);
        if (event.getStatus() == LiveEventStatus.FINALIZED || event.getStatus() == LiveEventStatus.CANCELLED) {
            throw new BlastScaleException(ErrorCode.EVENT_INVALID_STATE, "Event is already " + event.getStatus());
        }
        event.transition(LiveEventStatus.CANCELLED, Instant.now(clock));
        cache.evict(ACTIVE_KEY);
        return toView(event, true);
    }

    // ------------------------------------------------------------------ scheduler hooks

    /** SCHEDULED -> ACTIVE when the start time has passed. */
    @Transactional
    public int activateDue() {
        Instant now = Instant.now(clock);
        List<LiveEvent> due = events.findByStatusAndStartAtLessThanEqual(LiveEventStatus.SCHEDULED, now);
        due.forEach(e -> e.transition(LiveEventStatus.ACTIVE, now));
        if (!due.isEmpty()) {
            cache.evict(ACTIVE_KEY);
        }
        return due.size();
    }

    /** ACTIVE -> ENDED -> FINALIZED when the end time has passed. */
    @Transactional
    public int endDue() {
        Instant now = Instant.now(clock);
        List<LiveEvent> due = events.findByStatusAndEndAtLessThanEqual(LiveEventStatus.ACTIVE, now);
        for (LiveEvent event : due) {
            event.transition(LiveEventStatus.ENDED, now);
            finalizeEvent(event);
        }
        if (!due.isEmpty()) {
            cache.evict(ACTIVE_KEY);
        }
        return due.size();
    }

    /** ENDED events whose finalization crashed halfway are retried here. */
    @Transactional
    public int finalizePending() {
        List<LiveEvent> pending = events.findByStatusIn(List.of(LiveEventStatus.ENDED));
        pending.forEach(this::finalizeEvent);
        return pending.size();
    }

    /**
     * Pays prizes exactly once: each prize is a ledger credit referenced by {@code event:{id}}
     * (unique per player), and the status moves to FINALIZED only after all prizes are recorded.
     * Re-running for a partially finalized event skips the players already paid.
     */
    private void finalizeEvent(LiveEvent event) {
        Instant now = Instant.now(clock);
        int rewarded = 0;
        if (event.getType() == LiveEventType.ROCKET_RACE) {
            EventRule.RocketRaceRule rule = (EventRule.RocketRaceRule) ruleParser.parse(event.getType(), event.getConfiguration());
            int prizeCount = rule.rewards().isEmpty() ? 0 : rule.rewards().keySet().stream().max(Integer::compare).orElse(0);
            List<EventParticipation> top = participations.findByIdEventIdOrderByPointsDescUpdatedAtAsc(
                    event.getId(), PageRequest.of(0, Math.max(prizeCount, 1)));
            int rank = 1;
            String reference = "event:" + event.getId();
            for (EventParticipation participation : top) {
                int coins = rule.rewards().getOrDefault(rank, 0);
                long playerId = participation.getId().getPlayerId();
                if (coins > 0 && !economyService.wasApplied(playerId, TransactionReason.EVENT_REWARD, reference)) {
                    economyService.credit(playerId, Resource.COIN, coins, TransactionReason.EVENT_REWARD, reference);
                    telemetry.record(TelemetryEventType.EVENT_REWARD_GRANTED, playerId, "event", reference,
                            Map.of("rank", rank, "coins", coins, "points", participation.getPoints(), "event", event.getName()));
                    rewarded++;
                }
                participation.setFinalResult(rank, coins);
                rank++;
            }
        }
        event.transition(LiveEventStatus.FINALIZED, now);
        telemetry.record(TelemetryEventType.EVENT_FINALIZED, null, "event", Long.toString(event.getId()),
                Map.of("name", event.getName(), "type", event.getType().name(), "rewarded", rewarded));
        log.info("Finalized live event {} '{}': {} players rewarded", event.getId(), event.getName(), rewarded);
    }

    // ------------------------------------------------------------------ helpers

    private EventRule rule(LiveEventView view) {
        return ruleParser.parse(LiveEventType.valueOf(view.type()), objectMapper.writeValueAsString(view.configuration()));
    }

    private LiveEvent require(long id) {
        return events.findById(id)
                .orElseThrow(() -> new BlastScaleException(ErrorCode.EVENT_NOT_FOUND, "Event " + id + " does not exist"));
    }

    private List<LiveEventView.Standing> standings(long eventId, int limit) {
        List<EventParticipation> top = participations.findByIdEventIdOrderByPointsDescUpdatedAtAsc(eventId, PageRequest.of(0, limit));
        Map<Long, String> names = playerService.usernamesOf(top.stream().map(p -> p.getId().getPlayerId()).toList());
        List<LiveEventView.Standing> result = new ArrayList<>();
        int rank = 1;
        for (EventParticipation p : top) {
            long playerId = p.getId().getPlayerId();
            result.add(new LiveEventView.Standing(rank++, playerId, names.getOrDefault(playerId, "player" + playerId),
                    p.getPoints(), p.getRewardCoins()));
        }
        return result;
    }

    private LiveEventView toView(LiveEvent event, boolean withStandings) {
        Map<String, Object> configuration = objectMapper.readValue(event.getConfiguration(), MAP);
        return new LiveEventView(event.getId(), event.getType().name(), event.getName(), event.getStatus().name(),
                event.getStartAt(), event.getEndAt(), configuration,
                withStandings ? participations.countByIdEventId(event.getId()) : null,
                withStandings ? standings(event.getId(), TOP_SIZE) : null,
                event.getCreatedAt(), event.getUpdatedAt());
    }

    /** Cache envelope. */
    record ActiveEvents(List<LiveEventView> events) {
    }
}
