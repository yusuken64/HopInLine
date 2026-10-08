using HopInLine.Data.Line;
using Xunit;

namespace HopInLine.Tests;

/// <summary>
/// Tests for timer transitions and pause/resume logic.
/// Regression tests for bugs in position schemes and pause behavior.
/// </summary>
public class TimerTransitionTests : IDisposable
{
    private readonly SqliteDbFixture _fixture;
    private readonly SQLLineRepository _repository;
    private const int ToleranceSeconds = 2; // Tolerance for time-based assertions

    public TimerTransitionTests()
    {
        _fixture = new SqliteDbFixture();
        _repository = new SQLLineRepository(_fixture);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task FirstJoin_StartsTimerOnAutoAdvanceLine()
    {
        // Arrange: A new auto-advance line has no deadline yet
        var line = TestData.Line(id: "JOIN-1", autoAdvanceLine: true);
        await _repository.AddLineAsync(line, default);

        // Act
        await _repository.AddParticipantAsync("JOIN-1", TestData.Participant("Alice", 0));

        // Assert: The first person gets a full interval
        var retrieved = await _repository.GetLineAsync("JOIN-1");
        Assert.NotNull(retrieved?.NextAdvanceAt);
        var remaining = (retrieved.NextAdvanceAt.Value - DateTime.UtcNow).TotalSeconds;
        Assert.InRange(remaining, 30 - ToleranceSeconds, 30);
    }

    [Fact]
    public async Task LaterJoin_DoesNotResetTimer()
    {
        // Arrange: Someone is already mid-turn
        var deadline = DateTime.UtcNow.AddSeconds(10);
        var line = TestData.Line(id: "JOIN-2", autoAdvanceLine: true);
        line.NextAdvanceAt = deadline;
        line.Participants.Add(TestData.Participant("Alice", 1));
        line.NextPosition = 2;
        await _repository.AddLineAsync(line, default);

        // Act
        await _repository.AddParticipantAsync("JOIN-2", TestData.Participant("Bob", 0));

        // Assert: Bob joining didn't give Alice more time
        var retrieved = await _repository.GetLineAsync("JOIN-2");
        Assert.NotNull(retrieved?.NextAdvanceAt);
        Assert.True(retrieved.NextAdvanceAt.Value - DateTime.UtcNow < TimeSpan.FromSeconds(30 - ToleranceSeconds));
    }

    [Fact]
    public async Task Pause_StoresRemainingAndClearsDeadline()
    {
        // Arrange: Create a line with NextAdvanceAt set 30 seconds in the future
        var now = DateTime.UtcNow;
        var futureDeadline = now.AddSeconds(30);
        var line = TestData.Line(id: "PAUSE-1");
        line.NextAdvanceAt = futureDeadline;
        await _repository.AddLineAsync(line, default);

        // Act: Pause the timer
        await _repository.PauseTimerAsync("PAUSE-1");

        // Assert: Check NextAdvanceAt, IsPaused, and UnpauseRemaining
        var retrieved = await _repository.GetLineAsync("PAUSE-1");
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.NextAdvanceAt);
        Assert.True(retrieved.IsPaused);
        Assert.NotNull(retrieved.UnpauseRemaining);

        // UnpauseRemaining should be between ~25 and ~30 seconds
        var remainingSeconds = retrieved.UnpauseRemaining.Value.TotalSeconds;
        Assert.InRange(remainingSeconds, 25, 30);
    }

    [Fact]
    public async Task Resume_RestoresRemaining()
    {
        // Arrange: Create a paused line with UnpauseRemaining = 30 seconds
        var now = DateTime.UtcNow;
        var line = TestData.Line(id: "RESUME-1");
        line.IsPaused = true;
        line.NextAdvanceAt = null;
        line.UnpauseRemaining = TimeSpan.FromSeconds(30);
        line.CountDownStart = now;
        await _repository.AddLineAsync(line, default);

        // Act: Resume the timer
        await _repository.ResumeTimerAsync("RESUME-1");

        // Assert: NextAdvanceAt should be roughly 30 seconds in the future
        var retrieved = await _repository.GetLineAsync("RESUME-1");
        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.NextAdvanceAt);
        Assert.False(retrieved.IsPaused);
        Assert.Null(retrieved.UnpauseRemaining);

        var secondsUntilNext = (retrieved.NextAdvanceAt.Value - DateTime.UtcNow).TotalSeconds;
        // Should be approximately 30 seconds, not a full interval and not in the past
        Assert.InRange(secondsUntilNext, 28, 32);
    }

    [Fact]
    public async Task Pause_PastDeadline_ClampsToZero()
    {
        // Arrange: Create a line with deadline already 10 minutes past
        var now = DateTime.UtcNow;
        var pastDeadline = now.AddSeconds(-600); // 10 minutes past
        var line = TestData.Line(id: "PAUSE-PAST-1");
        line.NextAdvanceAt = pastDeadline;
        await _repository.AddLineAsync(line, default);

        // Act: Pause the timer
        await _repository.PauseTimerAsync("PAUSE-PAST-1");

        // Assert: UnpauseRemaining should be TimeSpan.Zero, never negative
        var retrieved = await _repository.GetLineAsync("PAUSE-PAST-1");
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.NextAdvanceAt);
        Assert.True(retrieved.IsPaused);
        Assert.NotNull(retrieved.UnpauseRemaining);
        Assert.Equal(TimeSpan.Zero, retrieved.UnpauseRemaining.Value);
    }

    [Fact]
    public async Task PausedLine_IsNotDue()
    {
        // Arrange: Create a line with old deadline set in the past, then pause it
        var now = DateTime.UtcNow;
        var pastDeadline = now.AddSeconds(-30); // 30 seconds past
        var line = TestData.Line(id: "PAUSE-NOTDUE-1", autoAdvanceLine: true);
        line.NextAdvanceAt = pastDeadline;

        // Add a participant so it's eligible for advancing
        var participant = TestData.Participant("Test", 0);
        await _repository.AddLineAsync(line, default);
        await _repository.AddParticipantAsync("PAUSE-NOTDUE-1", participant);

        // Pause the line
        await _repository.PauseTimerAsync("PAUSE-NOTDUE-1");

        // Act: Check if the line is due
        var dueLineIds = await _repository.GetDueLineIdsAsync(DateTime.UtcNow, default);

        // Assert: The paused line should not be due
        Assert.DoesNotContain("PAUSE-NOTDUE-1", dueLineIds);
    }

    [Fact]
    public async Task LineWithOnlyRemovedParticipants_IsNotDue()
    {
        // Arrange: Create a line with a past deadline and only removed participants
        var now = DateTime.UtcNow;
        var pastDeadline = now.AddSeconds(-30);
        var line = TestData.Line(id: "ONLY-REMOVED-1", autoAdvanceLine: true);
        line.NextAdvanceAt = pastDeadline;

        await _repository.AddLineAsync(line, default);

        // Add a participant and then remove it
        var participant = TestData.Participant("Test", 0);
        await _repository.AddParticipantAsync("ONLY-REMOVED-1", participant);
        await _repository.RemovedParticipantAsync("ONLY-REMOVED-1", participant.Id);

        // Act: Check if the line is due
        var dueLineIds = await _repository.GetDueLineIdsAsync(DateTime.UtcNow, default);

        // Assert: The line should not be due (no active participants)
        Assert.DoesNotContain("ONLY-REMOVED-1", dueLineIds);
    }

    [Fact]
    public async Task Restart_SetsFullInterval()
    {
        // Arrange: Create a line with no deadline
        var line = TestData.Line(id: "RESTART-1");
        line.NextAdvanceAt = null;
        line.AutoAdvanceInterval = TimeSpan.FromSeconds(30);
        await _repository.AddLineAsync(line, default);

        var beforeRestart = DateTime.UtcNow;

        // Act: Restart the timer
        await _repository.RestartTimerAsync("RESTART-1");

        var afterRestart = DateTime.UtcNow;

        // Assert: NextAdvanceAt should be roughly one full interval (30 seconds) in the future
        var retrieved = await _repository.GetLineAsync("RESTART-1");
        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.NextAdvanceAt);
        Assert.Null(retrieved.UnpauseRemaining);
        Assert.False(retrieved.IsPaused);

        var secondsUntilNext = (retrieved.NextAdvanceAt.Value - afterRestart).TotalSeconds;
        // Should be approximately 30 seconds
        Assert.InRange(secondsUntilNext, 28, 32);
    }

    [Fact]
    public async Task AutoRestartTimerOnAdvanceFalse_AdvancesOnceThenPauses()
    {
        // Arrange: Create a line with AutoRestartTimerOnAdvance = false, a future deadline, and a participant
        var now = DateTime.UtcNow;
        var futureDeadline = now.AddSeconds(1); // Very close to now so TryAdvanceAsync can succeed
        var line = TestData.Line(id: "NOAUTOREST-1", autoAdvanceLine: true, autoRestartTimerOnAdvance: false);
        line.NextAdvanceAt = futureDeadline;

        await _repository.AddLineAsync(line, default);
        var participant = TestData.Participant("Test", 0);
        await _repository.AddParticipantAsync("NOAUTOREST-1", participant);

        // Make the deadline definitely in the past so TryAdvanceAsync will succeed
        var retrieved = await _repository.GetLineAsync("NOAUTOREST-1");
        Assert.NotNull(retrieved);
        retrieved.NextAdvanceAt = DateTime.UtcNow.AddSeconds(-1);
        await _repository.UpdateLineAsync(retrieved);

        // Act: Try to advance
        var result = await _repository.TryAdvanceAsync("NOAUTOREST-1");

        // Assert: TryAdvanceAsync returns true
        Assert.True(result);

        // After advance, NextAdvanceAt should be null and IsPaused should be true
        retrieved = await _repository.GetLineAsync("NOAUTOREST-1");
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.NextAdvanceAt);
        Assert.True(retrieved.IsPaused);

        // The line should no longer be due
        var dueLineIds = await _repository.GetDueLineIdsAsync(DateTime.UtcNow, default);
        Assert.DoesNotContain("NOAUTOREST-1", dueLineIds);
    }

    [Fact]
    public async Task AutoAdvanceLineFalse_NeverDue()
    {
        // Arrange: Create a line with AutoAdvanceLine = false and a past deadline
        var now = DateTime.UtcNow;
        var pastDeadline = now.AddSeconds(-30);
        var line = TestData.Line(id: "NOAUTOADVANCE-1", autoAdvanceLine: false);
        line.NextAdvanceAt = pastDeadline;

        await _repository.AddLineAsync(line, default);
        var participant = TestData.Participant("Test", 0);
        await _repository.AddParticipantAsync("NOAUTOADVANCE-1", participant);

        // Act: Check if the line is due
        var dueLineIds = await _repository.GetDueLineIdsAsync(DateTime.UtcNow, default);

        // Assert: The line should never be due when AutoAdvanceLine is false
        Assert.DoesNotContain("NOAUTOADVANCE-1", dueLineIds);
    }
}
