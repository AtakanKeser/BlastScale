package com.atakan.blastscale.level;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/** Read-only level preview for the level map screen ("Level 12: 18 moves, target 980"). */
@RestController
@RequestMapping("/api/v1/levels")
public class LevelController {

    private final LevelDefinitionService levels;

    public LevelController(LevelDefinitionService levels) {
        this.levels = levels;
    }

    @GetMapping("/{levelNumber}")
    public LevelDefinition get(@PathVariable int levelNumber) {
        return levels.get(levelNumber);
    }
}
