package com.atakan.blastscale.remoteconfig;

import com.atakan.blastscale.remoteconfig.dto.ConfigEntryView;
import com.atakan.blastscale.remoteconfig.dto.UpdateConfigRequest;
import com.atakan.blastscale.security.CurrentPlayer;
import com.atakan.blastscale.security.PlayerPrincipal;
import jakarta.validation.Valid;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;
import tools.jackson.databind.ObjectMapper;

import java.util.List;

/** Remote configuration management for the LiveOps panel. */
@RestController
@RequestMapping("/api/v1/admin/config")
@PreAuthorize("hasRole('ADMIN')")
public class ConfigAdminController {

    private final RemoteConfigService remoteConfigService;
    private final ObjectMapper objectMapper;

    public ConfigAdminController(RemoteConfigService remoteConfigService, ObjectMapper objectMapper) {
        this.remoteConfigService = remoteConfigService;
        this.objectMapper = objectMapper;
    }

    @GetMapping
    public List<ConfigEntryView> list() {
        return remoteConfigService.listEntries().stream().map(this::toView).toList();
    }

    @PutMapping("/{key}")
    public ConfigEntryView update(@PathVariable String key, @Valid @RequestBody UpdateConfigRequest request,
                                  @CurrentPlayer PlayerPrincipal admin) {
        return toView(remoteConfigService.update(key, request.value(), request.description(), admin.username()));
    }

    private ConfigEntryView toView(RemoteConfigEntry entry) {
        Object value = objectMapper.readValue(entry.getValueJson(), Object.class);
        return new ConfigEntryView(entry.getKey(), value, entry.getDescription(), entry.getUpdatedAt(), entry.getUpdatedBy());
    }
}
