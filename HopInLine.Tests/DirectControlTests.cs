using HopInLine.Data.Line;
using Xunit;

namespace HopInLine.Tests;

/// <summary>
/// Manager direct-control actions: jump a participant to any place in the line, and rename them.
/// </summary>
public class DirectControlTests : IDisposable
{
    private readonly SqliteDbFixture _fixture;
    private readonly SQLLineRepository _repository;
    private readonly LineService _service;

    public DirectControlTests()
    {
        _fixture = new SqliteDbFixture();
        _repository = new SQLLineRepository(_fixture);
        _service = new LineService(_repository, new LineChangeNotifier(_fixture, Microsoft.Extensions.Logging.Abstractions.NullLogger<LineChangeNotifier>.Instance));
    }

    public void Dispose() => _fixture.Dispose();

    private async Task<(string LineId, string Token)> CreateLineWith(params string[] names)
    {
        var line = await _service.CreateLineAsync("Direct", null, false, false, TimeSpan.FromMinutes(1));
        foreach (var name in names)
        {
            await _service.AddParticipantAsync(line.Id, name);
        }
        return (line.Id, line.ManageToken);
    }

    private async Task<List<ParticipantDto>> Active(string lineId)
    {
        var line = await _service.GetLineAsync(lineId);
        Assert.NotNull(line);
        return line.Participants.OrderBy(p => p.Position).ToList();
    }

    private async Task<string> IdOf(string lineId, string name) =>
        (await Active(lineId)).Single(p => p.Name == name).Id;

    [Theory]
    [InlineData("D", 0, new[] { "D", "A", "B", "C" })]   // make up now
    [InlineData("D", 1, new[] { "A", "D", "B", "C" })]   // make up next
    [InlineData("A", 1, new[] { "B", "A", "C", "D" })]   // current person steps back one
    [InlineData("A", int.MaxValue, new[] { "B", "C", "D", "A" })] // send to back (clamped)
    [InlineData("C", -5, new[] { "C", "A", "B", "D" })]  // negative clamps to front
    [InlineData("B", 1, new[] { "A", "B", "C", "D" })]   // already there: no change
    public async Task MoveParticipantTo_PlacesParticipantAtIndex(string who, int index, string[] expected)
    {
        var (lineId, token) = await CreateLineWith("A", "B", "C", "D");

        await _service.MoveParticipantToAsync(lineId, token, await IdOf(lineId, who), index);

        Assert.Equal(expected, (await Active(lineId)).Select(p => p.Name));
    }

    [Fact]
    public async Task MoveParticipantTo_KeepsPositionsUniqueAndNextPositionUnchanged()
    {
        var (lineId, token) = await CreateLineWith("A", "B", "C");
        var before = await _repository.GetLineAsync(lineId);
        Assert.NotNull(before);
        var nextPosition = before.NextPosition;

        await _service.MoveParticipantToAsync(lineId, token, await IdOf(lineId, "C"), 0);
        // Joining afterwards still lands at the back
        await _service.AddParticipantAsync(lineId, "E");

        var after = await _repository.GetLineAsync(lineId);
        Assert.NotNull(after);
        var positions = after.Participants.Select(p => p.Position).ToList();
        Assert.Equal(positions.Count, positions.Distinct().Count());
        Assert.Equal(nextPosition + 1, after.NextPosition);
        Assert.Equal(new[] { "C", "A", "B", "E" }, (await Active(lineId)).Select(p => p.Name));
    }

    [Fact]
    public async Task MoveParticipantTo_IgnoresRemovedParticipants()
    {
        var (lineId, token) = await CreateLineWith("A", "B", "C");
        var bId = await IdOf(lineId, "B");
        await _service.RemoveParticipantAsync(lineId, token, bId);

        // Moving a removed participant is a no-op
        await _service.MoveParticipantToAsync(lineId, token, bId, 0);
        Assert.Equal(new[] { "A", "C" }, (await Active(lineId)).Select(p => p.Name));

        // Re-adding still puts them at the back
        await _service.ReAddRemovedParticipantAsync(lineId, token, bId);
        Assert.Equal(new[] { "A", "C", "B" }, (await Active(lineId)).Select(p => p.Name));
    }

    [Fact]
    public async Task RenameParticipant_TrimsAndSaves()
    {
        var (lineId, token) = await CreateLineWith("Alcie");

        await _service.RenameParticipantAsync(lineId, token, await IdOf(lineId, "Alcie"), "  Alice  ");

        Assert.Equal("Alice", (await Active(lineId)).Single().Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameParticipant_RejectsBlankNames(string name)
    {
        var (lineId, token) = await CreateLineWith("Alice");
        var id = await IdOf(lineId, "Alice");

        await Assert.ThrowsAsync<ArgumentException>(() => _service.RenameParticipantAsync(lineId, token, id, name));
        Assert.Equal("Alice", (await Active(lineId)).Single().Name);
    }

    [Fact]
    public async Task RenameParticipant_RejectsOverlongNames()
    {
        var (lineId, token) = await CreateLineWith("Alice");
        var id = await IdOf(lineId, "Alice");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RenameParticipantAsync(lineId, token, id, new string('x', 61)));
    }

    [Fact]
    public async Task RenameParticipant_CannotTouchAnotherLinesParticipant()
    {
        var (lineA, tokenA) = await CreateLineWith("Alice");
        var (lineB, _) = await CreateLineWith("Bob");
        var bobId = await IdOf(lineB, "Bob");

        // Valid token for line A, participant from line B
        await _service.RenameParticipantAsync(lineA, tokenA, bobId, "Hacked");

        Assert.Equal("Bob", (await Active(lineB)).Single().Name);
    }
}
