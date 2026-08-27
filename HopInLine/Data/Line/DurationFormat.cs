using System;
using System.Globalization;

namespace HopInLine.Data.Line;

/// <summary>
/// Formats TimeSpan durations for display, with rules that match JS-001's countdown formatter
/// so server-rendered labels and live JavaScript countdowns render identically.
/// </summary>
public static class DurationFormat
{
    /// <summary>
    /// Formats a TimeSpan into a human-readable duration string.
    /// - Zero or negative values format as "0:00".
    /// - Under one hour: "m:ss" (minutes not zero-padded, seconds always two digits).
    /// - One hour or more: "h:mm:ss" (all components zero-padded).
    /// - 24 hours or more: hours continue to count up rather than wrapping.
    /// </summary>
    public static string Humanize(TimeSpan value)
    {
        // Zero or negative values format as "0:00"
        if (value <= TimeSpan.Zero)
        {
            return "0:00";
        }

        // Get total seconds (truncated toward zero, never rounded up)
        long totalSeconds = (long)value.TotalSeconds;

        // Calculate minutes, hours, and seconds
        long hours = totalSeconds / 3600;
        long minutes = (totalSeconds % 3600) / 60;
        long seconds = totalSeconds % 60;

        if (hours == 0)
        {
            // Under one hour: m:ss format
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:d2}", minutes, seconds);
        }
        else
        {
            // One hour or more: h:mm:ss format
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:d2}:{2:d2}", hours, minutes, seconds);
        }
    }
}
