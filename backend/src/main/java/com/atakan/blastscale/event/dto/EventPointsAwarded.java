package com.atakan.blastscale.event.dto;

/** Returned to the level-completion flow so the client can show "+1 rocket". */
public record EventPointsAwarded(long eventId, String name, String type, long points, long totalPoints) {
}
