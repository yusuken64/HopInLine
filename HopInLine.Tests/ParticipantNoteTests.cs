using HopInLine.Data.Line;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HopInLine.Tests;

/// <summary>
/// Participant notes: anyone can set their own (leave token), the manager can set anybody's (manage token).
/// </summary>
public class ParticipantNoteTests : IDisposable
{
    private readonly SqliteDbFixture _fixture;
    private readonly LineService _service;

    public ParticipantNoteTests()
    {
        _fixture = new SqliteDbFixture();
        _service = new LineService(new SQLLineRepository(_fixture),
            new LineChangeNotifier(_fixture, NullLogger<LineChangeNotifier>.Instance));
    }

    public void Dispose() => _fixture.Dispose();

    private async Task<(string LineId, string ManageToken, ParticipantJoinResult Alice, ParticipantJoinResult Bob)> CreateLine()
    {
        var line = await _service.CreateLineAsync("Notes", null, false, false, TimeSpan.FromMinutes(1));
        var alice = await _service.AddParticipantAsync(line.Id, "Alice");
        var bob = await _service.AddParticipantAsync(line.Id, "Bob");
        return (line.Id, line.ManageToken, alice, bob);
    }

    private async Task<string?> NoteOf(string lineId, string participantId)
    {
        var line = await _service.GetLineAsync(lineId);
        Assert.NotNull(line);
        return line.Participants.Concat(line.RemovedParticipants).Single(p => p.Id == participantId).Description;
    }

    [Fact]
    public async Task SetOwnNote_WithOwnLeaveToken_SavesTrimmedNote()
    {
        var (lineId, _, alice, _) = await CreateLine();

        await _service.SetOwnNoteAsync(lineId, alice.ParticipantId, alice.LeaveToken, "  Question about HW3  ");

        Assert.Equal("Question about HW3", await NoteOf(lineId, alice.ParticipantId));
    }

    [Fact]
    public async Task SetOwnNote_WithSomeoneElsesLeaveToken_IsRejected()
    {
        var (lineId, _, alice, bob) = await CreateLine();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.SetOwnNoteAsync(lineId, alice.ParticipantId, bob.LeaveToken, "Hijacked"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.SetOwnNoteAsync(lineId, alice.ParticipantId, "", "Hijacked"));

        Assert.Null(await NoteOf(lineId, alice.ParticipantId));
    }

    [Fact]
    public async Task SetOwnNote_UnknownParticipant_Throws()
    {
        var (lineId, _, alice, _) = await CreateLine();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.SetOwnNoteAsync(lineId, "nobody", alice.LeaveToken, "Hi"));
    }

    [Fact]
    public async Task SetParticipantNote_ManagerCanSetAnybodysNote()
    {
        var (lineId, token, alice, bob) = await CreateLine();

        await _service.SetParticipantNoteAsync(lineId, token, alice.ParticipantId, "Needs a laptop charger");
        await _service.SetParticipantNoteAsync(lineId, token, bob.ParticipantId, "Group B");

        Assert.Equal("Needs a laptop charger", await NoteOf(lineId, alice.ParticipantId));
        Assert.Equal("Group B", await NoteOf(lineId, bob.ParticipantId));
    }

    [Fact]
    public async Task SetParticipantNote_RequiresManageToken()
    {
        var (lineId, _, alice, _) = await CreateLine();

        // A participant's leave token is not a manage token
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.SetParticipantNoteAsync(lineId, alice.LeaveToken, alice.ParticipantId, "Nope"));

        Assert.Null(await NoteOf(lineId, alice.ParticipantId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankNote_ClearsIt(string? blank)
    {
        var (lineId, token, alice, _) = await CreateLine();
        await _service.SetOwnNoteAsync(lineId, alice.ParticipantId, alice.LeaveToken, "Something");

        await _service.SetParticipantNoteAsync(lineId, token, alice.ParticipantId, blank);

        Assert.Null(await NoteOf(lineId, alice.ParticipantId));
    }

    [Fact]
    public async Task Note_Over200Characters_IsRejected()
    {
        var (lineId, token, alice, _) = await CreateLine();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetOwnNoteAsync(lineId, alice.ParticipantId, alice.LeaveToken, new string('x', 201)));
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetParticipantNoteAsync(lineId, token, alice.ParticipantId, new string('x', 201)));

        // Exactly 200 is fine
        await _service.SetParticipantNoteAsync(lineId, token, alice.ParticipantId, new string('x', 200));
        Assert.Equal(200, (await NoteOf(lineId, alice.ParticipantId))!.Length);
    }

    [Fact]
    public async Task SetParticipantNote_CannotTouchAnotherLinesParticipant()
    {
        var (lineA, tokenA, _, _) = await CreateLine();
        var (lineB, _, carol, _) = await CreateLine();

        await _service.SetParticipantNoteAsync(lineA, tokenA, carol.ParticipantId, "Hacked");

        Assert.Null(await NoteOf(lineB, carol.ParticipantId));
    }

    [Fact]
    public async Task Note_SurvivesAdvanceAndRotation()
    {
        var line = await _service.CreateLineAsync("Rotation", null, autoReAdd: true, false, TimeSpan.FromMinutes(1));
        var alice = await _service.AddParticipantAsync(line.Id, "Alice");
        await _service.AddParticipantAsync(line.Id, "Bob");
        await _service.SetOwnNoteAsync(line.Id, alice.ParticipantId, alice.LeaveToken, "Room 204");

        await _service.AdvanceLineAsync(line.Id, line.ManageToken);

        Assert.Equal("Room 204", await NoteOf(line.Id, alice.ParticipantId));
    }
}
