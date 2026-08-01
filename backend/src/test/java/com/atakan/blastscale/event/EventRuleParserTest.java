package com.atakan.blastscale.event;

import com.atakan.blastscale.common.exception.BlastScaleException;
import org.junit.jupiter.api.Test;
import tools.jackson.databind.ObjectMapper;

import java.util.Map;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

class EventRuleParserTest {

    private final EventRuleParser parser = new EventRuleParser(new ObjectMapper());

    @Test
    void parsesRocketRace() {
        EventRule rule = parser.parse(LiveEventType.ROCKET_RACE,
                "{\"pointsPerLevel\":2,\"minimumLevel\":20,\"rewards\":{\"1\":10000,\"2\":5000,\"3\":3000}}");
        assertThat(rule).isInstanceOf(EventRule.RocketRaceRule.class);
        EventRule.RocketRaceRule race = (EventRule.RocketRaceRule) rule;
        assertThat(race.pointsPerLevel()).isEqualTo(2);
        assertThat(race.minimumLevel()).isEqualTo(20);
        assertThat(race.rewards()).hasSize(3).containsAllEntriesOf(Map.of(1, 10000, 2, 5000, 3, 3000));
        assertThat(race.rewards().keySet()).containsExactly(1, 2, 3); // TreeMap: ranks stay ordered
    }

    @Test
    void rejectsBadRewardKeysAndMissingRewards() {
        assertThatThrownBy(() -> parser.parse(LiveEventType.ROCKET_RACE, "{\"rewards\":{\"gold\":1}}"))
                .isInstanceOf(BlastScaleException.class);
        assertThatThrownBy(() -> parser.parse(LiveEventType.ROCKET_RACE, "{}"))
                .isInstanceOf(BlastScaleException.class);
    }

    @Test
    void doubleRewardDefaultsAndBounds() {
        assertThat(((EventRule.DoubleRewardRule) parser.parse(LiveEventType.DOUBLE_REWARD, "{}")).multiplier()).isEqualTo(2.0);
        assertThat(((EventRule.DoubleRewardRule) parser.parse(LiveEventType.DOUBLE_REWARD, "{\"multiplier\":3}")).multiplier()).isEqualTo(3.0);
        assertThatThrownBy(() -> parser.parse(LiveEventType.DOUBLE_REWARD, "{\"multiplier\":0.5}")).isInstanceOf(BlastScaleException.class);
        assertThatThrownBy(() -> parser.parse(LiveEventType.DOUBLE_REWARD, "not json")).isInstanceOf(BlastScaleException.class);
    }
}
