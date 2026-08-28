using HopInLine.Data.Line;
using Xunit;

namespace HopInLine.Tests;

/// <summary>
/// Authorization tests for RemoveSelfAsync. This operation uses a distinct authorization axis
/// (participant leave token) separate from the line-level manage token tested in AuthorizationTests.cs.
/// This file also guards against LeaveToken ever leaking onto ParticipantDto.
/// </summary>
public class SelfLeaveTests : IDisposable
{
    private readonly SqliteDbFixture _fixture;
    private readonly SQLLineRepository _repository;
    private readonly RecordingLineChangeNotifier _notifier;
    private readonly LineService _service;

    public SelfLeaveTests()
    {
        _fixture = new SqliteDbFixture();
        _notifier = new RecordingLineChangeNotifier();
        _repository = new SQLLineRepository(_fixture);
        _service = new LineService(_repository, _notifier, new NoOpLineOwnerNotifier(), new NoOpEmailRateLimiter());
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    /// <summary>
    /// No-op owner notifier for testing LineService.
    /// </summary>
    private class NoOpLineOwnerNotifier : ILineOwnerNotifier
    {
        public Task NotifyLineCreatedAsync(string ownerEmail, string lineName, string viewUrl, string manageUrl, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// No-op email rate limiter for testing LineService.
    /// </summary>
    private class NoOpEmailRateLimiter : IEmailRateLimiter
    {
        public bool TryConsume(string email)
        {
            return true;
        }
    }

    /// <summary>
    /// Stub notifier that records which line IDs were notified.
    /// </summary>
    private class RecordingLineChangeNotifier : ILineChangeNotifier
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

        private class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Creates a line with a known leaveToken and a single participant for use in mutation tests.
    /// </summary>
    private async Task<(string lineId, string participantId)> CreateLineWithParticipant(string correctLeaveToken, bool removed = false)
    {
        var line = TestData.Line();
        var participant = TestData.Participant("Alice", 1, removed: removed, leaveToken: correctLeaveToken);
        line.Participants.Add(participant);

        using (var context = _fixture.CreateContext())
        {
            context.Lines.Add(line);
            await context.SaveChangesAsync();
        }

        return (line.Id, participant.Id);
    }

    [Fact]
    public async Task RemoveSelfAsync_WrongToken_ThrowsUnauthorized()
    {
        // Arrange a line+participant with leaveToken "correct-token"
        var (lineId, participantId) = await CreateLineWithParticipant("correct-token");

        // Call RemoveSelfAsync with the wrong token
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.RemoveSelfAsync(lineId, participantId, "wrong-token")
        );

        // Assert: _notifier.NotifiedLineIds is empty
        Assert.Empty(_notifier.NotifiedLineIds);
    }

    [Fact]
    public async Task RemoveSelfAsync_EmptyToken_ThrowsUnauthorized()
    {
        // Arrange a line+participant with leaveToken "correct-token"
        var (lineId, participantId) = await CreateLineWithParticipant("correct-token");

        // Call RemoveSelfAsync with empty token
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.RemoveSelfAsync(lineId, participantId, "")
        );

        // Assert: _notifier.NotifiedLineIds is empty
        Assert.Empty(_notifier.NotifiedLineIds);
    }

    [Fact]
    public async Task RemoveSelfAsync_CorrectToken_RemovesParticipantAndNotifies()
    {
        // Arrange a line+participant with leaveToken "correct-token"
        var (lineId, participantId) = await CreateLineWithParticipant("correct-token");

        // Call RemoveSelfAsync with the correct token
        await _service.RemoveSelfAsync(lineId, participantId, "correct-token");

        // Assert: no exception was thrown
        // Assert: _notifier.NotifiedLineIds contains lineId
        Assert.Contains(lineId, _notifier.NotifiedLineIds);

        // Assert: re-fetching the line via _repository shows the participant's Removed == true
        using (var context = _fixture.CreateContext())
        {
            var retrievedLine = await _repository.GetLineAsync(lineId);
            Assert.NotNull(retrievedLine);
            var removedParticipant = retrievedLine.Participants.FirstOrDefault(p => p.Id == participantId);
            Assert.NotNull(removedParticipant);
            Assert.True(removedParticipant.Removed);
        }
    }

    [Fact]
    public async Task RemoveSelfAsync_AlreadyRemoved_IsIdempotentNoOp()
    {
        // Arrange with removed: true
        var (lineId, participantId) = await CreateLineWithParticipant("correct-token", removed: true);

        // Call RemoveSelfAsync with the correct token
        await _service.RemoveSelfAsync(lineId, participantId, "correct-token");

        // Assert: no exception is thrown
        // Assert: _notifier.NotifiedLineIds is empty (no-op means no notify)
        Assert.Empty(_notifier.NotifiedLineIds);
    }

    [Fact]
    public async Task RemoveSelfAsync_MissingLine_ThrowsKeyNotFound()
    {
        // Call RemoveSelfAsync with a nonexistent line ID
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.RemoveSelfAsync("NOSUCHLINE", "any-id", "any-token")
        );
    }

    [Fact]
    public async Task RemoveSelfAsync_MissingParticipant_ThrowsKeyNotFound()
    {
        // Arrange a line with one participant
        var (lineId, _) = await CreateLineWithParticipant("correct-token");

        // Call RemoveSelfAsync with a nonexistent participant ID
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.RemoveSelfAsync(lineId, "nonexistent-participant-id", "correct-token")
        );
    }

    [Fact]
    public void ParticipantDto_NeverExposesLeaveToken()
    {
        // Assert: LeaveToken property must not exist on ParticipantDto
        Assert.Null(typeof(ParticipantDto).GetProperty("LeaveToken"));
    }
}
