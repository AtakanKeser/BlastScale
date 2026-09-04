using System;
using System.Globalization;

namespace BlastScale.Client.Core
{
    /// <summary>Small formatting helpers for countdowns and the ISO-8601 instants the server sends.</summary>
    public static class TimeFormat
    {
        /// <summary>"m:ss" under an hour, "h:mm:ss" above; never negative.</summary>
        public static string Countdown(long seconds)
        {
            if (seconds < 0) seconds = 0;
            long hours = seconds / 3600;
            long minutes = (seconds % 3600) / 60;
            long secs = seconds % 60;
            return hours > 0
                ? hours + ":" + minutes.ToString("00") + ":" + secs.ToString("00")
                : minutes + ":" + secs.ToString("00");
        }

        /// <summary>Coarser variant for long durations ("2d 3h", "5h 12m", "45m").</summary>
        public static string Duration(long seconds)
        {
            if (seconds < 60) return seconds + "s";
            long days = seconds / 86400;
            long hours = (seconds % 86400) / 3600;
            long minutes = (seconds % 3600) / 60;
            if (days > 0) return days + "d " + hours + "h";
            if (hours > 0) return hours + "h " + minutes + "m";
            return minutes + "m";
        }

        /// <summary>Parses an ISO-8601 instant ("2026-09-04T12:00:00Z"); null when absent or malformed.</summary>
        public static DateTime? ParseInstant(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return null;
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime value))
            {
                return value;
            }
            return null;
        }

        /// <summary>Thousands separators for scores and coins ("12,345").</summary>
        public static string Number(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>"★★☆" style star rating out of three.</summary>
        public static string Stars(int stars)
        {
            string result = "";
            for (int i = 0; i < 3; i++)
            {
                result += i < stars ? "★" : "☆";
            }
            return result;
        }
    }
}
