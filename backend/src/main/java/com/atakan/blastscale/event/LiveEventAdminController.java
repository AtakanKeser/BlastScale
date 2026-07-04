package com.atakan.blastscale.event;

import com.atakan.blastscale.event.dto.CreateEventRequest;
import com.atakan.blastscale.event.dto.LiveEventView;
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

/** LiveOps: create, start, end and cancel events without deploying anything. */
@RestController
@RequestMapping("/api/v1/admin/events")
@PreAuthorize("hasRole('ADMIN')")
public class LiveEventAdminController {

    private final LiveEventService eventService;

    public LiveEventAdminController(LiveEventService eventService) {
        this.eventService = eventService;
    }

    @GetMapping
    public List<LiveEventView> list() {
        return eventService.listAll();
    }

    @GetMapping("/{id}")
    public LiveEventView get(@PathVariable long id) {
        return eventService.get(id);
    }

    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    public LiveEventView create(@Valid @RequestBody CreateEventRequest request) {
        return eventService.create(request);
    }

    @PostMapping("/{id}/activate")
    public LiveEventView activate(@PathVariable long id) {
        return eventService.activate(id);
    }

    @PostMapping("/{id}/end")
    public LiveEventView end(@PathVariable long id) {
        return eventService.end(id);
    }

    @PostMapping("/{id}/cancel")
    public LiveEventView cancel(@PathVariable long id) {
        return eventService.cancel(id);
    }
}
