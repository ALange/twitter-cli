// Tests for TimeUtil - mirrors test_timeutil.py
using XCliSharp;
using Xunit;

namespace XCliSharp.Tests;

public class TimeUtilTests
{
    private const string SampleTimestamp = "Sat Mar 08 12:00:00 +0000 2026";

    [Fact]
    public void FormatLocalTime_ValidTimestamp_ReturnsFormattedDate()
    {
        var result = TimeUtil.FormatLocalTime(SampleTimestamp);
        // Should be in "yyyy-MM-dd HH:mm" format
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$", result);
    }

    [Fact]
    public void FormatLocalTime_EmptyString_ReturnsOriginal()
    {
        var result = TimeUtil.FormatLocalTime("");
        Assert.Equal("", result);
    }

    [Fact]
    public void FormatLocalTime_InvalidTimestamp_ReturnsOriginal()
    {
        var result = TimeUtil.FormatLocalTime("not-a-date");
        Assert.Equal("not-a-date", result);
    }

    [Fact]
    public void FormatIso8601_ValidTimestamp_ReturnsIsoFormat()
    {
        var result = TimeUtil.FormatIso8601(SampleTimestamp);
        // ISO 8601 format like "2026-03-08T12:00:00+00:00"
        Assert.Contains("2026-03-08", result);
        Assert.Contains("12:00:00", result);
    }

    [Fact]
    public void FormatIso8601_EmptyString_ReturnsOriginal()
    {
        var result = TimeUtil.FormatIso8601("");
        Assert.Equal("", result);
    }

    [Fact]
    public void FormatRelativeTime_PastTimestamp_ReturnsPastRelative()
    {
        // Create a timestamp 5 minutes ago
        var fiveMinutesAgo = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("ddd MMM dd HH:mm:ss zzz yyyy",
            System.Globalization.CultureInfo.InvariantCulture);
        var result = TimeUtil.FormatRelativeTime(fiveMinutesAgo);
        Assert.Contains("m ago", result);
    }

    [Fact]
    public void FormatRelativeTime_FarFutureTimestamp_ReturnsJustNow()
    {
        // A future timestamp
        var future = DateTimeOffset.UtcNow.AddHours(1).ToString("ddd MMM dd HH:mm:ss zzz yyyy",
            System.Globalization.CultureInfo.InvariantCulture);
        var result = TimeUtil.FormatRelativeTime(future);
        Assert.Equal("just now", result);
    }

    [Fact]
    public void FormatRelativeTime_InvalidTimestamp_ReturnsOriginal()
    {
        var result = TimeUtil.FormatRelativeTime("bad-timestamp");
        Assert.Equal("bad-timestamp", result);
    }

    [Theory]
    [InlineData(30, "30s ago")]
    public void FormatRelativeTime_RecentPast_ReturnsSeconds(int secondsAgo, string expected)
    {
        var ts = DateTimeOffset.UtcNow.AddSeconds(-secondsAgo).ToString("ddd MMM dd HH:mm:ss zzz yyyy",
            System.Globalization.CultureInfo.InvariantCulture);
        var result = TimeUtil.FormatRelativeTime(ts);
        Assert.Equal(expected, result);
    }
}
