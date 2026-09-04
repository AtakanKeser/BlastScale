package com.atakan.blastscale.event;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.common.exception.ErrorCode;
import org.springframework.stereotype.Component;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.util.Iterator;
import java.util.Map;
import java.util.TreeMap;

/** Validates and parses the free-form configuration JSON of an event into an {@link EventRule}. */
@Component
public class EventRuleParser {

    private final ObjectMapper objectMapper;

    public EventRuleParser(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    public EventRule parse(LiveEventType type, String json) {
        JsonNode node;
        try {
            node = objectMapper.readTree(json == null ? "{}" : json);
        } catch (RuntimeException e) {
            throw new BlastScaleException(ErrorCode.EVENT_INVALID_CONFIGURATION, "Configuration is not valid JSON");
        }
        return switch (type) {
            case ROCKET_RACE -> {
                int pointsPerLevel = node.path("pointsPerLevel").asInt(1);
                int minimumLevel = node.path("minimumLevel").asInt(1);
                Map<Integer, Integer> rewards = new TreeMap<>();
                JsonNode rewardsNode = node.path("rewards");
                for (Iterator<Map.Entry<String, JsonNode>> it = rewardsNode.properties().iterator(); it.hasNext(); ) {
                    Map.Entry<String, JsonNode> entry = it.next();
                    try {
                        rewards.put(Integer.parseInt(entry.getKey()), entry.getValue().asInt());
                    } catch (NumberFormatException e) {
                        throw new BlastScaleException(ErrorCode.EVENT_INVALID_CONFIGURATION,
                                "rewards keys must be ranks, got '" + entry.getKey() + "'");
                    }
                }
                if (pointsPerLevel <= 0 || rewards.isEmpty()) {
                    throw new BlastScaleException(ErrorCode.EVENT_INVALID_CONFIGURATION,
                            "ROCKET_RACE needs pointsPerLevel > 0 and at least one reward");
                }
                yield new EventRule.RocketRaceRule(pointsPerLevel, minimumLevel, rewards);
            }
            case DOUBLE_REWARD -> {
                double multiplier = node.path("multiplier").asDouble(2.0);
                if (multiplier <= 1.0 || multiplier > 10.0) {
                    throw new BlastScaleException(ErrorCode.EVENT_INVALID_CONFIGURATION, "multiplier must be in (1, 10]");
                }
                yield new EventRule.DoubleRewardRule(multiplier);
            }
        };
    }
}
