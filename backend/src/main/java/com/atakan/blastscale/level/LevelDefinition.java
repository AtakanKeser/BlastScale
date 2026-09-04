package com.atakan.blastscale.level;

import com.atakan.blastscale.level.engine.BoardConfig;
import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.mapping.Document;

import java.time.Instant;
import java.util.List;
import java.util.Map;

/**
 * Level configuration document stored in MongoDB.
 *
 * <p>Level content is naturally document shaped (nested objectives, free-form special rules that
 * differ per level, versioned) and never joined with player state, which is why it lives in a
 * document store rather than MySQL. The {@code id} is {@code level-{number}} so lookups are
 * primary-key reads without any extra index.
 */
@Document(collection = "levels")
public class LevelDefinition {

    @Id
    private String id;
    private int levelNumber;
    private int version;
    private int rows;
    private int cols;
    private int colorCount;
    private int moveLimit;
    private int targetScore;
    private List<Integer> starThresholds;
    private Map<String, Object> specialRules;
    private String source;
    private Instant updatedAt;

    public LevelDefinition() {
    }

    public LevelDefinition(int levelNumber, int version, int rows, int cols, int colorCount, int moveLimit,
                           int targetScore, List<Integer> starThresholds, Map<String, Object> specialRules,
                           String source, Instant updatedAt) {
        this.id = idFor(levelNumber);
        this.levelNumber = levelNumber;
        this.version = version;
        this.rows = rows;
        this.cols = cols;
        this.colorCount = colorCount;
        this.moveLimit = moveLimit;
        this.targetScore = targetScore;
        this.starThresholds = starThresholds;
        this.specialRules = specialRules == null ? Map.of() : specialRules;
        this.source = source;
        this.updatedAt = updatedAt;
    }

    public static String idFor(int levelNumber) {
        return "level-" + levelNumber;
    }

    /** The engine's view of this level. */
    public BoardConfig toBoardConfig() {
        return new BoardConfig(rows, cols, colorCount, moveLimit, targetScore, starThresholds);
    }

    public String getId() {
        return id;
    }

    public int getLevelNumber() {
        return levelNumber;
    }

    public int getVersion() {
        return version;
    }

    public int getRows() {
        return rows;
    }

    public int getCols() {
        return cols;
    }

    public int getColorCount() {
        return colorCount;
    }

    public int getMoveLimit() {
        return moveLimit;
    }

    public int getTargetScore() {
        return targetScore;
    }

    public List<Integer> getStarThresholds() {
        return starThresholds;
    }

    public Map<String, Object> getSpecialRules() {
        return specialRules;
    }

    public String getSource() {
        return source;
    }

    public Instant getUpdatedAt() {
        return updatedAt;
    }
}
