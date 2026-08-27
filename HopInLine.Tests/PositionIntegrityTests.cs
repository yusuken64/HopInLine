using HopInLine.Data.Line;
using Xunit;

namespace HopInLine.Tests;

/// <summary>
/// Tests for position integrity — verifying the unified NextPosition++ rule
/// and regression tests for the two colliding position schemes.
/// </summary>
public class PositionIntegrityTests : IDisposable
{
    private readonly SqliteDbFixture _fixture;
    private readonly SQLLineRepository _repository;

    public PositionIntegrityTests()
    {
        _fixture = new SqliteDbFixture();
        _repository = new SQLLineRepository(_fixture);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task AddParticipant_AssignsSequentialPositions()
    {
        // Arrange: Create a line and add three participants
        var line = TestData.Line(id: "POSITIONS-1");
        await _repository.AddLineAsync(line, default);

        // Act: Add three participants
        var p1 = TestData.Participant("Alice", 0);
        var p2 = TestData.Participant("Bob", 0);
        var p3 = TestData.Participant("Charlie", 0);

        await _repository.AddParticipantAsync("POSITIONS-1", p1);
        await _repository.AddParticipantAsync("POSITIONS-1", p2);
        await _repository.AddParticipantAsync("POSITIONS-1", p3);

        // Assert: Positions are sequential starting at 1, NextPosition is 4
        var retrieved = await _repository.GetLineAsync("POSITIONS-1");
        Assert.NotNull(retrieved);
        var activeParticipants = retrieved.Participants
            .Where(p => !p.Removed)
            .OrderBy(p => p.Position)
            .ToList();

        Assert.Equal(3, activeParticipants.Count);
        Assert.Equal(1, activeParticipants[0].Position);
        Assert.Equal(2, activeParticipants[1].Position);
        Assert.Equal(3, activeParticipants[2].Position);
        Assert.Equal(4, retrieved.NextPosition);
    }

    [Fact]
    public async Task AddThenAdvanceThenAdd_NeverDuplicatesPosition()
    {
        // Arrange: Create a line with AutoReAdd = true
        var line = TestData.Line(id: "REGA-1", autoReAdd: true);
        await _repository.AddLineAsync(line, default);

        // Add two participants
        var p1 = TestData.Participant("Alice", 0);
        var p2 = TestData.Participant("Bob", 0);
        await _repository.AddParticipantAsync("REGA-1", p1);
        await _repository.AddParticipantAsync("REGA-1", p2);

        var retrieved = await _repository.GetLineAsync("REGA-1");
        Assert.NotNull(retrieved);
        var alice = retrieved.Participants.FirstOrDefault(x => x.Name == "Alice");
        var bob = retrieved.Participants.FirstOrDefault(x => x.Name == "Bob");
        Assert.NotNull(alice);
        Assert.NotNull(bob);
        Assert.Equal(1, alice.Position);
        Assert.Equal(2, bob.Position);

        // Act: Advance the line (Alice moves to end via AutoReAdd)
        await _repository.AdvanceLineAsync("REGA-1");

        // Get the state after advance
        retrieved = await _repository.GetLineAsync("REGA-1");
        Assert.NotNull(retrieved);
        var aliceAfterAdvance = retrieved.Participants.FirstOrDefault(x => x.Name == "Alice");
        var bobAfterAdvance = retrieved.Participants.FirstOrDefault(x => x.Name == "Bob");

        Assert.NotNull(aliceAfterAdvance);
        Assert.NotNull(bobAfterAdvance);

        // Alice should have been re-added with a new position (NextPosition was incremented twice, so now 3)
        // Bob should keep his original position
        Assert.Equal(2, bobAfterAdvance.Position);
        Assert.Equal(3, aliceAfterAdvance.Position);

        // Add a third participant
        var p3 = TestData.Participant("Charlie", 0);
        await _repository.AddParticipantAsync("REGA-1", p3);

        // Assert: All active participants have distinct positions
        retrieved = await _repository.GetLineAsync("REGA-1");
        Assert.NotNull(retrieved);
        var activeParticipants = retrieved.Participants
            .Where(p => !p.Removed)
            .OrderBy(p => p.Position)
            .ToList();

        Assert.Equal(3, activeParticipants.Count);
        var positions = activeParticipants.Select(p => p.Position).ToList();
        Assert.Equal(positions.Count, positions.Distinct().Count()); // All distinct
        Assert.Equal(5, retrieved.NextPosition);
    }

    [Fact]
    public async Task AutoReAdd_CyclesFairly()
    {
        // Arrange: Create a line with AutoReAdd = true
        var line = TestData.Line(id: "CYCLE-1", autoReAdd: true);
        await _repository.AddLineAsync(line, default);

        var pA = TestData.Participant("A", 0);
        var pB = TestData.Participant("B", 0);
        var pC = TestData.Participant("C", 0);

        await _repository.AddParticipantAsync("CYCLE-1", pA);
        await _repository.AddParticipantAsync("CYCLE-1", pB);
        await _repository.AddParticipantAsync("CYCLE-1", pC);

        var served = new List<string>();

        // Act & Assert: Advance three times
        // First advance: A at position 1 should be served
        await _repository.AdvanceLineAsync("CYCLE-1");
        var state = await _repository.GetLineAsync("CYCLE-1");
        Assert.NotNull(state);
        var active = state.Participants.Where(p => !p.Removed).OrderBy(p => p.Position).ToList();
        Assert.Equal(3, active.Count);
        served.Add(active.First().Name); // Should be B

        // Second advance: B at position 1 should be served
        await _repository.AdvanceLineAsync("CYCLE-1");
        state = await _repository.GetLineAsync("CYCLE-1");
        Assert.NotNull(state);
        active = state.Participants.Where(p => !p.Removed).OrderBy(p => p.Position).ToList();
        Assert.Equal(3, active.Count);
        served.Add(active.First().Name); // Should be C

        // Third advance: C at position 1 should be served
        await _repository.AdvanceLineAsync("CYCLE-1");
        state = await _repository.GetLineAsync("CYCLE-1");
        Assert.NotNull(state);
        active = state.Participants.Where(p => !p.Removed).OrderBy(p => p.Position).ToList();
        Assert.Equal(3, active.Count);
        served.Add(active.First().Name); // Should be A

        // Assert: Served order is B, C, A (each was at position 1 when served)
        Assert.Equal(3, served.Count);
        Assert.Equal("B", served[0]);
        Assert.Equal("C", served[1]);
        Assert.Equal("A", served[2]);

        // Verify no one was served consecutively
        for (int i = 0; i < served.Count - 1; i++)
        {
            Assert.NotEqual(served[i], served[i + 1]);
        }
    }

    [Fact]
    public async Task ReAddRemovedParticipant_GoesToBack()
    {
        // Arrange: Create a line with two participants
        var line = TestData.Line(id: "READD-1", autoReAdd: false);
        await _repository.AddLineAsync(line, default);

        var p1 = TestData.Participant("Alice", 0);
        var p2 = TestData.Participant("Bob", 0);
        await _repository.AddParticipantAsync("READD-1", p1);
        await _repository.AddParticipantAsync("READD-1", p2);

        var retrieved = await _repository.GetLineAsync("READD-1");
        Assert.NotNull(retrieved);
        var alice = retrieved.Participants.First(x => x.Name == "Alice");
        var aliceId = alice.Id;

        // Act: Remove the front participant (Alice)
        await _repository.RemovedParticipantAsync("READD-1", aliceId);

        // Re-add it
        await _repository.ReAddRemovedParticipantAsync("READD-1", aliceId);

        // Assert: Alice's position is greater than Bob's
        retrieved = await _repository.GetLineAsync("READD-1");
        Assert.NotNull(retrieved);
        var aliceAfterReAdd = retrieved.Participants.First(x => x.Name == "Alice");
        var bobAfter = retrieved.Participants.First(x => x.Name == "Bob");

        Assert.False(aliceAfterReAdd.Removed);
        Assert.True(aliceAfterReAdd.Position > bobAfter.Position);
    }

    [Fact]
    public async Task AdvanceEmptyLine_DoesNotThrow()
    {
        // Arrange: Create a line with no participants
        var line = TestData.Line(id: "EMPTY-1");
        await _repository.AddLineAsync(line, default);

        var retrieved = await _repository.GetLineAsync("EMPTY-1");
        Assert.NotNull(retrieved);
        var initialNextPosition = retrieved.NextPosition;

        // Act & Assert: Advancing should not throw
        await _repository.AdvanceLineAsync("EMPTY-1");

        // Verify NextPosition is unchanged
        retrieved = await _repository.GetLineAsync("EMPTY-1");
        Assert.NotNull(retrieved);
        Assert.Equal(initialNextPosition, retrieved.NextPosition);
    }
}
