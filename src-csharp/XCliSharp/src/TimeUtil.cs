// Time formatting utilities for XCliSharp.
// Mirrors twitter_cli/timeutil.py

namespace XCliSharp;

public static class TimeUtil
{
    // Twitter API timestamp format: "Sat Mar 08 12:00:00 +0000 2026"
    // Parts[0]=DayName, [1]=Month, [2]=Day, [3]=Time, [4]=TzOffset, [5]=Year
    private const string DateWithoutTzFormat = "MMM dd HH:mm:ss yyyy";

    private static DateTimeOffset? ParseTwitterTime(string? createdAt)
    {
        if (string.IsNullOrEmpty(createdAt)) return null;
        try
        {
            var parts = createdAt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6) return null;

            // Reconstruct parseable string: "Mar 08 12:00:00 2026"
            var dateStr = $"{parts[1]} {parts[2]} {parts[3]} {parts[5]}";
            if (!DateTime.TryParseExact(dateStr, DateWithoutTzFormat,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dtUnspecified))
                return null;

            // Parse timezone offset: supports "+0000" (Twitter) and "+00:00" (.NET zzz)
            var tzStr = parts[4].Replace(":", ""); // normalize "+00:00" → "+0000"
            if (tzStr.Length < 5) return null;
            int sign = tzStr[0] == '+' ? 1 : -1;
            int tzH = int.Parse(tzStr[1..3]);
            int tzM = int.Parse(tzStr[3..5]);
            var offset = new TimeSpan(sign * tzH, sign * tzM, 0);

            return new DateTimeOffset(dtUnspecified, offset);
        }
        catch { return null; }
    }

    /// <summary>Convert Twitter timestamp to local time string like "2026-03-14 21:08".</summary>
    public static string FormatLocalTime(string createdAt)
    {
        var dt = ParseTwitterTime(createdAt);
        if (dt is null) return createdAt;
        return dt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    /// <summary>Convert Twitter timestamp to a relative time string like "2m ago".</summary>
    public static string FormatRelativeTime(string createdAt)
    {
        var dt = ParseTwitterTime(createdAt);
        if (dt is null) return createdAt;

        var now = DateTimeOffset.UtcNow;
        var delta = now - dt.Value;
        var seconds = (int)delta.TotalSeconds;

        if (seconds < 0) return "just now";
        if (seconds < 60) return $"{seconds}s ago";
        var minutes = seconds / 60;
        if (minutes < 60) return $"{minutes}m ago";
        var hours = minutes / 60;
        if (hours < 24) return $"{hours}h ago";
        var days = hours / 24;
        if (days < 30) return $"{days}d ago";
        var months = days / 30;
        if (months < 12) return $"{months}mo ago";
        var years = days / 365;
        return $"{years}y ago";
    }

    /// <summary>Convert Twitter timestamp to ISO 8601 format.</summary>
    public static string FormatIso8601(string createdAt)
    {
        var dt = ParseTwitterTime(createdAt);
        if (dt is null) return createdAt;
        return dt.Value.ToString("o");
    }
}
