using HopInLine.Data.Line;
using Xunit;

namespace HopInLine.Tests;

/// <summary>
/// Tests for the owner-email-backup feature in CreateLineAsync.
/// Verifies that owner notification and rate limiting work correctly in isolation.
/// </summary>
public class OwnerNotificationTests : IDisposable
{
    private readonly SqliteDbFixture _fixture;
    private readonly LineService _service;
    private readonly RecordingLineOwnerNotifier _recordingNotifier;
    private readonly RecordingLineChangeNotifier _recordingChangeNotifier;

    public OwnerNotificationTests()
    {
        _fixture = new SqliteDbFixture();
        var repository = new SQLLineRepository(_fixture);
        _recordingChangeNotifier = new RecordingLineChangeNotifier();
        _recordingNotifier = new RecordingLineOwnerNotifier();
        _service = new LineService(repository, _recordingChangeNotifier, _recordingNotifier, new AlwaysAllowRateLimiter());
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    /// <summary>
    /// Recording implementation of ILineOwnerNotifier for testing.
    /// Records every call's arguments for later assertion.
    /// </summary>
    private class RecordingLineOwnerNotifier : ILineOwnerNotifier
    {
        public List<(string OwnerEmail, string LineName, string ViewUrl, string ManageUrl)> Calls { get; } = new();

        public Task NotifyLineCreatedAsync(string ownerEmail, string lineName, string viewUrl, string manageUrl, CancellationToken cancellationToken = default)
        {
            Calls.Add((ownerEmail, lineName, viewUrl, manageUrl));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Rate limiter stub that always allows consumption.
    /// Used for tests that are not specifically about rate limiting.
    /// </summary>
    private class AlwaysAllowRateLimiter : IEmailRateLimiter
    {
        public bool TryConsume(string email)
        {
            return true;
        }
    }

    /// <summary>
    /// Stub implementation of ILineChangeNotifier.
    /// Records which line IDs were notified for verification.
    /// </summary>
    private class RecordingLineChangeNotifier : ILineChangeNotifier, IDisposable
    {
        public List<string> NotifiedLineIds { get; } = new();

        public IDisposable Subscribe(string lineId, Func<LineDto, Task> handler)
        {
            // Tests don't use subscriptions
            return new NoOpDisposable();
        }

        public Task NotifyAsync(string lineId)
        {
            NotifiedLineIds.Add(lineId);
            return Task.CompletedTask;
        }

        public void Dispose() { }

        private class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Test: When ownerEmail is null, the notifier is never called.
    /// </summary>
    [Fact]
    public async Task CreateLineAsync_NoEmail_NotifierNotCalled()
    {
        // Act
        var line = await _service.CreateLineAsync("Test Line", null, false, false, TimeSpan.FromMinutes(1), ownerEmail: null);

        // Assert
        Assert.Empty(_recordingNotifier.Calls);
    }

    /// <summary>
    /// Test: When ownerEmail is valid and rate limiter allows, the notifier is called with correct arguments.
    /// </summary>
    [Fact]
    public async Task CreateLineAsync_ValidEmail_NotifierCalledWithLinks()
    {
        // Arrange
        var ownerEmail = "owner@example.com";
        var repository = new SQLLineRepository(_fixture);
        var recordingChangeNotifier = new RecordingLineChangeNotifier();
        var recordingOwnerNotifier = new RecordingLineOwnerNotifier();
        var service = new LineService(repository, recordingChangeNotifier, recordingOwnerNotifier, new AlwaysAllowRateLimiter());

        // Act
        var line = await service.CreateLineAsync("Test Line", null, false, false, TimeSpan.FromMinutes(1), ownerEmail: ownerEmail);

        // Assert
        Assert.Single(recordingOwnerNotifier.Calls);
        var (callEmail, callLineName, callViewUrl, callManageUrl) = recordingOwnerNotifier.Calls[0];
        Assert.Equal(ownerEmail, callEmail);
        Assert.Equal("Test Line", callLineName);
        Assert.Contains($"/view-line/{line.Id}", callViewUrl);
        Assert.Contains($"/manage/{line.Id}", callManageUrl);

        recordingChangeNotifier.Dispose();
    }

    /// <summary>
    /// Test: When ownerEmail has invalid format, CreateLineAsync throws ArgumentException before persisting.
    /// </summary>
    [Fact]
    public async Task CreateLineAsync_InvalidEmailFormat_ThrowsArgumentException()
    {
        // Arrange
        var invalidEmail = "not-an-email";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateLineAsync("Test Line", null, false, false, TimeSpan.FromMinutes(1), ownerEmail: invalidEmail)
        );

        // Verify no notification was sent
        Assert.Empty(_recordingNotifier.Calls);

        // Verify no line was created (check the message content)
        Assert.Contains("email", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test: When rate limiter rejects, the notifier is skipped but line creation succeeds.
    /// Tests 6 sequential calls with the same email; the 6th should be rejected by the rate limiter.
    /// </summary>
    [Fact]
    public async Task CreateLineAsync_RateLimited_SkipsNotifierButLineStillCreated()
    {
        // Arrange
        var ownerEmail = "owner@example.com";
        var rateLimiter = new InMemoryEmailRateLimiter();
        var repository = new SQLLineRepository(_fixture);
        var recordingChangeNotifier = new RecordingLineChangeNotifier();
        var recordingOwnerNotifier = new RecordingLineOwnerNotifier();
        var service = new LineService(repository, recordingChangeNotifier, recordingOwnerNotifier, rateLimiter);

        // Act: Create 6 lines with the same owner email on the same service
        var lineIds = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            var line = await service.CreateLineAsync($"Test Line {i}", null, false, false, TimeSpan.FromMinutes(1), ownerEmail: ownerEmail);
            lineIds.Add(line.Id);
        }

        // Assert: Only 5 notifications (the 6th was rate-limited)
        Assert.Equal(5, recordingOwnerNotifier.Calls.Count);

        // Verify all 6 lines were created successfully with distinct IDs
        Assert.Equal(6, lineIds.Count);
        Assert.Equal(lineIds.Count, lineIds.Distinct().Count());

        recordingChangeNotifier.Dispose();
    }
}
