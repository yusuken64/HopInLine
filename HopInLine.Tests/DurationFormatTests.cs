using System;
using Xunit;
using HopInLine.Data.Line;

namespace HopInLine.Tests;

public class DurationFormatTests
{
    /// <summary>
    /// Theory test covering the formatting rules for various TimeSpan inputs.
    /// Test cases are specified as total seconds to avoid TimeSpan as an attribute argument.
    /// </summary>
    [Theory]
    [InlineData(0, "0:00")]                    // TimeSpan.Zero
    [InlineData(5, "0:05")]                    // 5 seconds
    [InlineData(65, "1:05")]                   // 65 seconds (1m 5s)
    [InlineData(545, "9:05")]                  // 9m 5s (9*60 + 5)
    [InlineData(3599, "59:59")]                // 59m 59s
    [InlineData(3600, "1:00:00")]              // 60 minutes (1 hour)
    [InlineData(5400, "1:30:00")]              // 90 minutes
    [InlineData(90000, "25:00:00")]            // 25 hours (25*3600)
    [InlineData(-30, "0:00")]                  // -30 seconds (negative)
    public void Humanize_FormatsTimeSpanCorrectly(long totalSeconds, string expected)
    {
        // Arrange
        var timespan = TimeSpan.FromSeconds(totalSeconds);

        // Act
        var result = DurationFormat.Humanize(timespan);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Regression test: 25 hours should not format the same as 1 hour.
    /// This locks down the specific bug fix: hours should count up past 24, not wrap using value.Hours component.
    /// </summary>
    [Fact]
    public void Humanize_DoesNotWrapAtTwentyFourHours()
    {
        // Arrange
        var oneHour = TimeSpan.FromSeconds(3600);
        var twentyFiveHours = TimeSpan.FromSeconds(90000);

        // Act
        var oneHourResult = DurationFormat.Humanize(oneHour);
        var twentyFiveHoursResult = DurationFormat.Humanize(twentyFiveHours);

        // Assert
        Assert.NotEqual(oneHourResult, twentyFiveHoursResult);
        Assert.Equal("1:00:00", oneHourResult);
        Assert.Equal("25:00:00", twentyFiveHoursResult);
    }
}
