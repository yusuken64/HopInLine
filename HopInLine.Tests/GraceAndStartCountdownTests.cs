using HopInLine.Data.Line;
using Xunit;

namespace HopInLine.Tests;

/// <summary>
/// Tests for grace time (gap between auto-advances) and start countdown (lead-in on manager Resume/Restart).
/// </summary>
public class GraceAndStartCountdownTests : IDisposable
{
    private readonly SqliteDbFixture _fixture;
    private readonly SQLLineRepository _repository;
    private readonly LineService _service;
    private const int Tolerance = 2;

    public GraceAndStartCountdownTests()
    {
        _fixture = new SqliteDbFixture();
        _repository = new SQLLineRepository(_fixture);
        _service = new LineService(_repository, new NoOpNotifier());
    }

    public void Dispose() => _fixture.Dispose();

    private class NoOpNotifier : ILineChangeNotifier
    {
        public IDisposable Subscribe(string lineId, Func<LineDto, Task> handler) => new NoOpDisposable();
        public Task NotifyAsync(string lineId) => Task.CompletedTask;
        private class NoOpDisposable : IDisposable { public void Dispose() { } }
    }

    private async Task<Line> SeedDueLineAsync(string id, TimeSpan grace, TimeSpan startCountdown, bool autoRestart = true)
    {
        var line = TestData.Line(id: id, autoAdvanceLine: true, autoReAdd: true, autoRestartTimerOnAdvance: autoRestart);
        line.GraceTime = grace;
        line.StartCountdown = startCountdown;
        line.NextAdvanceAt = DateTime.UtcNow.AddSeconds(-1);
        line.Participants.Add(TestData.Participant("Alice", 1));
        line.Participants.Add(TestData.Participant("Bob", 2));
        line.NextPosition = 3;
        await _repository.AddLineAsync(line, default);
        return line;
    }

    [Fact]
    public async Task Advance_ZeroGrace_SchedulesFullIntervalWithNoDelay()
    {
        await SeedDueLineAsync("G-0", TimeSpan.Zero, TimeSpan.Zero);

        Assert.True(await _repository.TryAdvanceAsync("G-0"));

        var after = await _repository.GetLineAsync("G-0");
        Assert.Null(after!.DelayEndsAt);
        Assert.InRange((after.NextAdvanceAt!.Value - DateTime.UtcNow).TotalSeconds, 30 - Tolerance, 30);
    }

    [Fact]
    public async Task Advance_WithGrace_AddsGraceBeforeInterval()
    {
        await SeedDueLineAsync("G-30", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));

        Assert.True(await _repository.TryAdvanceAsync("G-30"));

        var after = await _repository.GetLineAsync("G-30");
        Assert.InRange((after!.NextAdvanceAt!.Value - DateTime.UtcNow).TotalSeconds, 40 - Tolerance, 40);
        Assert.NotNull(after.DelayEndsAt);
        Assert.InRange((after.DelayEndsAt!.Value - DateTime.UtcNow).TotalSeconds, 10 - Tolerance, 10);
        Assert.False(after.DelayIsStartCountdown);
    }

    [Fact]
    public async Task Advance_WithoutAutoRestart_HasNoDeadlineOrDelay()
    {
        await SeedDueLineAsync("G-NR", TimeSpan.FromSeconds(10), TimeSpan.Zero, autoRestart: false);

        Assert.True(await _repository.TryAdvanceAsync("G-NR"));

        var after = await _repository.GetLineAsync("G-NR");
        Assert.Null(after!.NextAdvanceAt);
        Assert.Null(after.DelayEndsAt);
        Assert.True(after.IsPaused);
    }

    [Fact]
    public async Task Resume_WithStartCountdown_AddsLeadInOnly()
    {
        var line = TestData.Line(id: "S-RES", autoAdvanceLine: true);
        line.IsPaused = true;
        line.UnpauseRemaining = TimeSpan.FromSeconds(20);
        line.GraceTime = TimeSpan.FromSeconds(10);
        line.StartCountdown = TimeSpan.FromSeconds(5);
        await _repository.AddLineAsync(line, default);

        await _repository.ResumeTimerAsync("S-RES");

        var after = await _repository.GetLineAsync("S-RES");
        // 5s start countdown + 20s remaining; the 10s grace time is not used
        Assert.InRange((after!.NextAdvanceAt!.Value - DateTime.UtcNow).TotalSeconds, 25 - Tolerance, 25);
        Assert.InRange((after.DelayEndsAt!.Value - DateTime.UtcNow).TotalSeconds, 5 - Tolerance, 5);
        Assert.True(after.DelayIsStartCountdown);
    }

    [Fact]
    public async Task Restart_WithStartCountdown_AddsLeadIn()
    {
        var line = TestData.Line(id: "S-RST", autoAdvanceLine: true);
        line.StartCountdown = TimeSpan.FromSeconds(5);
        await _repository.AddLineAsync(line, default);

        await _repository.RestartTimerAsync("S-RST");

        var after = await _repository.GetLineAsync("S-RST");
        Assert.InRange((after!.NextAdvanceAt!.Value - DateTime.UtcNow).TotalSeconds, 35 - Tolerance, 35);
        Assert.True(after.DelayIsStartCountdown);
    }

    [Fact]
    public async Task FirstJoin_IgnoresBothDelays()
    {
        var line = TestData.Line(id: "J-0", autoAdvanceLine: true);
        line.GraceTime = TimeSpan.FromSeconds(10);
        line.StartCountdown = TimeSpan.FromSeconds(5);
        await _repository.AddLineAsync(line, default);

        await _repository.AddParticipantAsync("J-0", TestData.Participant("Alice", 0));

        var after = await _repository.GetLineAsync("J-0");
        Assert.Null(after!.DelayEndsAt);
        Assert.InRange((after.NextAdvanceAt!.Value - DateTime.UtcNow).TotalSeconds, 30 - Tolerance, 30);
    }

    [Fact]
    public async Task Pause_ClearsDelay()
    {
        var line = TestData.Line(id: "P-D", autoAdvanceLine: true);
        line.NextAdvanceAt = DateTime.UtcNow.AddSeconds(40);
        line.DelayEndsAt = DateTime.UtcNow.AddSeconds(10);
        await _repository.AddLineAsync(line, default);

        await _repository.PauseTimerAsync("P-D");

        Assert.Null((await _repository.GetLineAsync("P-D"))!.DelayEndsAt);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(300, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 300)]
    public async Task Create_RejectsOutOfRangeDelays(int graceSeconds, int startSeconds)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.CreateLineAsync(
            "Line", null, false, true, TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(graceSeconds), TimeSpan.FromSeconds(startSeconds)));
    }

    [Fact]
    public async Task Create_StoresDelays_AndZeroesThemWhenNotAutoAdvance()
    {
        var timed = await _service.CreateLineAsync("Timed", null, false, true, TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(10), timed.GraceTime);
        Assert.Equal(TimeSpan.FromSeconds(3), timed.StartCountdown);

        var simple = await _service.CreateLineAsync("Simple", null, false, false, TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.Zero, simple.GraceTime);
        Assert.Equal(TimeSpan.Zero, simple.StartCountdown);
    }

    [Fact]
    public async Task Update_StoresDelays_AndZeroesThemWhenAutoAdvanceTurnedOff()
    {
        var created = await _service.CreateLineAsync("Timed", null, false, true, TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.Zero, created.GraceTime); // defaults to 0

        await _service.UpdateLineAsync(created.Id, created.ManageToken, "Timed", null, true, false, true,
            TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(4));
        var updated = await _service.GetLineAsync(created.Id);
        Assert.Equal(TimeSpan.FromSeconds(15), updated!.GraceTime);
        Assert.Equal(TimeSpan.FromSeconds(4), updated.StartCountdown);

        await _service.UpdateLineAsync(created.Id, created.ManageToken, "Timed", null, false, false, true,
            TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(4));
        var off = await _service.GetLineAsync(created.Id);
        Assert.Equal(TimeSpan.Zero, off!.GraceTime);
        Assert.Equal(TimeSpan.Zero, off.StartCountdown);
    }
}
