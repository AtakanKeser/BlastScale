package com.atakan.blastscale.experiment;

import com.atakan.blastscale.experiment.dto.CreateExperimentRequest;
import com.atakan.blastscale.experiment.dto.ExperimentView;
import jakarta.validation.Valid;
import org.springframework.http.HttpStatus;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

/** Experiment management for the LiveOps panel. */
@RestController
@RequestMapping("/api/v1/admin/experiments")
@PreAuthorize("hasRole('ADMIN')")
public class ExperimentAdminController {

    private final ExperimentService experimentService;

    public ExperimentAdminController(ExperimentService experimentService) {
        this.experimentService = experimentService;
    }

    @GetMapping
    public List<ExperimentView> list() {
        return experimentService.listAll();
    }

    @GetMapping("/{id}")
    public ExperimentView get(@PathVariable long id) {
        return experimentService.get(id);
    }

    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    public ExperimentView create(@Valid @RequestBody CreateExperimentRequest request) {
        return experimentService.create(request);
    }

    @PostMapping("/{id}/start")
    public ExperimentView start(@PathVariable long id) {
        return experimentService.transition(id, ExperimentStatus.RUNNING);
    }

    @PostMapping("/{id}/pause")
    public ExperimentView pause(@PathVariable long id) {
        return experimentService.transition(id, ExperimentStatus.PAUSED);
    }

    @PostMapping("/{id}/end")
    public ExperimentView end(@PathVariable long id) {
        return experimentService.transition(id, ExperimentStatus.ENDED);
    }
}
