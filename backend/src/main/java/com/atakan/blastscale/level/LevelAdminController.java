package com.atakan.blastscale.level;

import com.atakan.blastscale.level.dto.UpsertLevelRequest;
import jakarta.validation.Valid;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

/** Level design endpoints for the admin panel. */
@RestController
@RequestMapping("/api/v1/admin/levels")
@PreAuthorize("hasRole('ADMIN')")
public class LevelAdminController {

    private final LevelDefinitionService levels;

    public LevelAdminController(LevelDefinitionService levels) {
        this.levels = levels;
    }

    @GetMapping
    public List<LevelDefinition> list(@RequestParam(defaultValue = "1") int from,
                                      @RequestParam(defaultValue = "50") int to) {
        return levels.list(from, Math.min(to, from + 200));
    }

    @PutMapping("/{levelNumber}")
    public LevelDefinition upsert(@PathVariable int levelNumber, @Valid @RequestBody UpsertLevelRequest request) {
        return levels.upsert(levelNumber, request);
    }
}
