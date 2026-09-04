package com.atakan.blastscale.leaderboard;

import java.time.DayOfWeek;
import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneOffset;
import java.time.temporal.IsoFields;
import java.time.temporal.TemporalAdjusters;

/**
 * Weekly seasons identified by ISO week, e.g. {@code 2026-W36}. A season starts Monday 00:00 UTC
 * and ends the following Monday 00:00 UTC. Pure functions, so every replica agrees on the
 * current season without coordination.
 */
public final class LeaderboardSeason {

    private LeaderboardSeason() {
    }

    public static String at(Instant instant) {
        LocalDate date = instant.atZone(ZoneOffset.UTC).toLocalDate();
        int year = date.get(IsoFields.WEEK_BASED_YEAR);
        int week = date.get(IsoFields.WEEK_OF_WEEK_BASED_YEAR);
        return String.format("%d-W%02d", year, week);
    }

    /** First instant of the season (Monday 00:00 UTC). */
    public static Instant startOf(String season) {
        int year = Integer.parseInt(season.substring(0, 4));
        int week = Integer.parseInt(season.substring(6));
        LocalDate monday = LocalDate.of(year, 1, 4) // Jan 4th is always in ISO week 1
                .with(IsoFields.WEEK_OF_WEEK_BASED_YEAR, week)
                .with(TemporalAdjusters.previousOrSame(DayOfWeek.MONDAY));
        return monday.atStartOfDay(ZoneOffset.UTC).toInstant();
    }

    /** First instant after the season. */
    public static Instant endOf(String season) {
        return startOf(season).plusSeconds(7 * 24 * 3600);
    }

    public static String previous(String season) {
        return at(startOf(season).minusSeconds(1));
    }

    public static boolean isValid(String season) {
        return season != null && season.matches("\\d{4}-W\\d{2}");
    }
}
