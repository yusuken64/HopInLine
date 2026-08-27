using HopInLine.Data.Line;

namespace HopInLine.Tests;

/// <summary>
/// Static builders for test data with sensible defaults and optional overrides.
/// </summary>
public static class TestData
{
    /// <summary>
    /// Creates a valid Line with sensible defaults and optional overrides.
    /// </summary>
    public static Line Line(
        string id = "TESTLINE",
        bool autoAdvanceLine = false,
        bool autoReAdd = false,
        bool autoRestartTimerOnAdvance = true,
        TimeSpan? interval = null,
        string manageToken = "test-token")
    {
        return new Line
        {
            Id = id,
            Name = "Test Line",
            Description = null,
            LastUpdated = DateTime.UtcNow,
            AutoAdvanceLine = autoAdvanceLine,
            AutoAdvanceInterval = interval ?? TimeSpan.FromSeconds(30),
            CountDownStart = DateTime.UtcNow,
            IsPaused = false,
            AutoReAdd = autoReAdd,
            AutoRestartTimerOnAdvance = autoRestartTimerOnAdvance,
            UnpauseRemaining = null,
            NextAdvanceAt = null,
            ManageToken = manageToken,
            Version = Guid.NewGuid(),
            Participants = new(),
            NextPosition = 1
        };
    }

    /// <summary>
    /// Creates a valid Participant with a fixed colour so assertions are deterministic.
    /// </summary>
    public static Participant Participant(string name, int position, bool removed = false)
    {
        return new Participant
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = null,
            Color = "#FF5733",  // Fixed deterministic color
            TurnCount = 0,
            Position = position,
            Removed = removed,
            LineId = string.Empty,
            Line = null!
        };
    }
}
