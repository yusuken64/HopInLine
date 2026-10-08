using HopInLine.Data.Line;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HopInLine.Tests;

/// <summary>
/// Rate limiting (per line, per connection, per IP) and idle-line cleanup.
/// </summary>
public class AbuseLimitTests : IDisposable
{
    private readonly SqliteDbFixture _fixture;
    private readonly SQLLineRepository _repository;
    private readonly InMemoryActionRateLimiter _limiter;
    private readonly LineService _service;

    public AbuseLimitTests()
    {
        _fixture = new SqliteDbFixture();
        _repository = new SQLLineRepository(_fixture);
        _limiter = new InMemoryActionRateLimiter();
        _service = new LineService(_repository, new NoOpLineChangeNotifier(), _limiter, new ClientContext { ClientIp = "10.0.0.1" });
    }

    public void Dispose()
    {
        _limiter.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public async Task Joins_Beyond_The_Per_Line_Limit_Are_Rejected()
    {
        await SeedLineAsync(TestData.Line(id: "RATE-001"));

        for (int i = 0; i < InMemoryActionRateLimiter.MaxJoinsPerLinePerMinute; i++)
        {
            await _service.AddParticipantAsync("RATE-001", $"Person {i}");
        }

        await Assert.ThrowsAsync<RateLimitExceededException>(() => _service.AddParticipantAsync("RATE-001", "One too many"));
    }

    [Fact]
    public async Task Join_Limit_Is_Per_Line()
    {
        await SeedLineAsync(TestData.Line(id: "RATE-A"));
        await SeedLineAsync(TestData.Line(id: "RATE-B"));

        for (int i = 0; i < InMemoryActionRateLimiter.MaxJoinsPerLinePerMinute; i++)
        {
            await _service.AddParticipantAsync("RATE-A", $"Person {i}");
        }

        // A different line still accepts joins
        var result = await _service.AddParticipantAsync("RATE-B", "Someone");
        Assert.False(string.IsNullOrEmpty(result.ParticipantId));
    }

    [Fact]
    public async Task Manager_Adds_Are_Exempt_From_Join_Limit()
    {
        await SeedLineAsync(TestData.Line(id: "RATE-MGR", manageToken: "mgr-token"));

        for (int i = 0; i < InMemoryActionRateLimiter.MaxJoinsPerLinePerMinute + 5; i++)
        {
            await _service.AddParticipantAsync("RATE-MGR", $"Person {i}", "mgr-token");
        }

        var line = await _repository.GetLineAsync("RATE-MGR");
        Assert.Equal(InMemoryActionRateLimiter.MaxJoinsPerLinePerMinute + 5, line!.Participants.Count);
    }

    [Fact]
    public async Task Wrong_Manage_Token_Does_Not_Bypass_Join_Limit()
    {
        await SeedLineAsync(TestData.Line(id: "RATE-BAD", manageToken: "real-token"));

        for (int i = 0; i < InMemoryActionRateLimiter.MaxJoinsPerLinePerMinute; i++)
        {
            await _service.AddParticipantAsync("RATE-BAD", $"Person {i}", "guess");
        }

        await Assert.ThrowsAsync<RateLimitExceededException>(() => _service.AddParticipantAsync("RATE-BAD", "One too many", "guess"));
    }

    [Fact]
    public async Task Idle_Lines_Are_Deleted_With_Their_Participants_And_Active_Lines_Are_Kept()
    {
        var idle = TestData.Line(id: "IDLE-001");
        idle.LastUpdated = DateTime.UtcNow.AddDays(-31);
        idle.Participants.Add(TestData.Participant("Idle person", 1));
        await SeedLineAsync(idle);

        var active = TestData.Line(id: "ACTIVE-001");
        active.LastUpdated = DateTime.UtcNow.AddDays(-1);
        await SeedLineAsync(active);

        var deleted = await _repository.DeleteLinesIdleSinceAsync(DateTime.UtcNow.AddDays(-30), CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Null(await _repository.GetLineAsync("IDLE-001"));
        Assert.NotNull(await _repository.GetLineAsync("ACTIVE-001"));
        using var context = _fixture.CreateContext();
        Assert.False(await context.Participants.AnyAsync(p => p.LineId == "IDLE-001"));
    }

    [Fact]
    public async Task Auto_Advance_Does_Not_Count_As_Activity()
    {
        var stale = DateTime.UtcNow.AddDays(-10);
        var line = TestData.Line(id: "AUTO-IDLE", autoAdvanceLine: true, interval: TimeSpan.FromMinutes(1));
        line.LastUpdated = stale;
        line.NextAdvanceAt = DateTime.UtcNow.AddSeconds(-5);
        line.Participants.Add(TestData.Participant("First", 1));
        line.Participants.Add(TestData.Participant("Second", 2));
        line.NextPosition = 3;
        await SeedLineAsync(line);

        Assert.True(await _repository.TryAdvanceAsync("AUTO-IDLE"));

        var after = await _repository.GetLineAsync("AUTO-IDLE");
        Assert.Equal(stale, after!.LastUpdated, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task User_Activity_Refreshes_LastUpdated()
    {
        var line = TestData.Line(id: "USER-ACT");
        line.LastUpdated = DateTime.UtcNow.AddDays(-10);
        await SeedLineAsync(line);

        await _service.AddParticipantAsync("USER-ACT", "Someone");

        var after = await _repository.GetLineAsync("USER-ACT");
        Assert.True(after!.LastUpdated > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Line_Creation_Is_Rate_Limited_Per_Connection()
    {
        var limit = InMemoryActionRateLimiter.ConnectionLimit(RateLimitedAction.Create);
        for (int i = 0; i < limit; i++)
        {
            await _service.CreateLineAsync($"Line {i}", null, false, false, TimeSpan.FromMinutes(5));
        }

        await Assert.ThrowsAsync<RateLimitExceededException>(
            () => _service.CreateLineAsync("One too many", null, false, false, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task Manage_Token_Guessing_Is_Rate_Limited()
    {
        await SeedLineAsync(TestData.Line(id: "GUESS-01", manageToken: "real-token"));

        var limit = InMemoryActionRateLimiter.ConnectionLimit(RateLimitedAction.Read);
        for (int i = 0; i < limit; i++)
        {
            Assert.False(await _service.ValidateManageTokenAsync("GUESS-01", $"guess-{i}"));
        }

        // Throttled callers get an error, not a silent false
        await Assert.ThrowsAsync<RateLimitExceededException>(() => _service.ValidateManageTokenAsync("GUESS-01", "real-token"));
    }

    [Fact]
    public async Task Manager_Actions_Are_Rate_Limited()
    {
        await SeedLineAsync(TestData.Line(id: "MGR-SPAM", manageToken: "mgr-token"));

        var limit = InMemoryActionRateLimiter.ConnectionLimit(RateLimitedAction.Write);
        for (int i = 0; i < limit; i++)
        {
            await _service.PauseTimerAsync("MGR-SPAM", "mgr-token");
        }

        await Assert.ThrowsAsync<RateLimitExceededException>(() => _service.PauseTimerAsync("MGR-SPAM", "mgr-token"));
    }

    [Fact]
    public async Task Each_Connection_Has_Its_Own_Budget()
    {
        var limit = InMemoryActionRateLimiter.ConnectionLimit(RateLimitedAction.Create);
        for (int i = 0; i < limit; i++)
        {
            await _service.CreateLineAsync($"Line {i}", null, false, false, TimeSpan.FromMinutes(5));
        }

        // Another tab from the same office IP is still under the looser per-IP limit
        var otherTab = new LineService(_repository, new NoOpLineChangeNotifier(),
            _limiter, new ClientContext { ClientIp = "10.0.0.1" });
        var line = await otherTab.CreateLineAsync("From another tab", null, false, false, TimeSpan.FromMinutes(5));
        Assert.NotNull(line);
    }

    /// <summary>Inserts a line keeping its LastUpdated as given (normal saves stamp it with now).</summary>
    private async Task SeedLineAsync(Line line)
    {
        using var context = _fixture.CreateContext();
        context.PreserveLastUpdated = true;
        context.Lines.Add(line);
        await context.SaveChangesAsync();
    }

    private class NoOpLineChangeNotifier : ILineChangeNotifier
    {
        public IDisposable Subscribe(string lineId, Func<LineDto, Task> handler) => new NoOpDisposable();
        public Task NotifyAsync(string lineId) => Task.CompletedTask;

        private class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
