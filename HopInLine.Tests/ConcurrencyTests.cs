using System;
using System.Data;
using System.Data.Common;
using HopInLine.Data.Line;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HopInLine.Tests;

/// <summary>
/// Tests proving that concurrent advances produce exactly one advance.
/// These tests verify that TryAdvanceAsync's in-transaction deadline re-check
/// and Version concurrency token prevent lost updates and duplicate advances.
/// </summary>
public class ConcurrencyTests : IDisposable
{
    private readonly SqliteDbFixture _fixture;

    public ConcurrencyTests()
    {
        _fixture = new SqliteDbFixture();
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    /// <summary>
    /// Test 1: SequentialAdvance_SecondCallReturnsFalse
    /// Arrange a due line, await TryAdvanceAsync twice in sequence, assert true then false,
    /// and assert the queue advanced exactly once by checking the front participant.
    /// </summary>
    [Fact]
    public async Task SequentialAdvance_SecondCallReturnsFalse()
    {
        // Arrange: Create a line with three participants, due immediately, auto-advance off
        var line = TestData.Line(id: "SEQ-001", autoAdvanceLine: true, autoReAdd: true, autoRestartTimerOnAdvance: false);
        line.NextAdvanceAt = DateTime.UtcNow.AddSeconds(-1); // Past deadline
        line.NextPosition = 4; // Ensure NextPosition is beyond the initial 3 participants

        var p1 = TestData.Participant("Alice", position: 1);
        var p2 = TestData.Participant("Bob", position: 2);
        var p3 = TestData.Participant("Charlie", position: 3);

        using (var context = _fixture.CreateContext())
        {
            line.Participants.Add(p1);
            line.Participants.Add(p2);
            line.Participants.Add(p3);
            context.Lines.Add(line);
            await context.SaveChangesAsync();
        }

        var repository = new SQLLineRepository(_fixture);

        // Act: Try advancing twice sequentially
        var firstResult = await repository.TryAdvanceAsync("SEQ-001");
        var secondResult = await repository.TryAdvanceAsync("SEQ-001");

        // Assert: First succeeds, second fails
        Assert.True(firstResult, "First advance should return true");
        Assert.False(secondResult, "Second advance should return false");

        // Verify exactly one participant advanced: p1 should have moved, and Bob (p2) is now front
        using (var context = _fixture.CreateContext())
        {
            var advancedLine = await context.Lines
                .Include(l => l.Participants)
                .FirstAsync(l => l.Id == "SEQ-001");

            var frontParticipant = advancedLine.Participants
                .Where(x => !x.Removed)
                .OrderBy(x => x.Position)
                .First();

            Assert.Equal("Bob", frontParticipant.Name);
            Assert.Equal(3, advancedLine.Participants.Count(p => !p.Removed)); // All 3 stay active with AutoReAdd=true
        }
    }

    /// <summary>
    /// Test 2: ConcurrentAdvance_AdvancesExactlyOnce
    /// Arrange a due line with three participants and AutoReAdd = true.
    /// Launch 5 concurrent TryAdvanceAsync calls with Task.WhenAll.
    /// Assert exactly one returned true, and the front participant moved by exactly one position.
    /// Catch DbUpdateConcurrencyException and SQLite lock errors as losing outcomes (treat as false).
    /// </summary>
    [Fact]
    public async Task ConcurrentAdvance_AdvancesExactlyOnce()
    {
        // Arrange: Create a line with three participants, due immediately, auto-reAdd enabled
        var line = TestData.Line(id: "CONC-001", autoAdvanceLine: true, autoReAdd: true, autoRestartTimerOnAdvance: false);
        line.NextAdvanceAt = DateTime.UtcNow.AddSeconds(-1); // Past deadline
        line.NextPosition = 4; // Ensure NextPosition is beyond the initial 3 participants

        var p1 = TestData.Participant("Alice", position: 1);
        var p2 = TestData.Participant("Bob", position: 2);
        var p3 = TestData.Participant("Charlie", position: 3);

        using (var context = _fixture.CreateContext())
        {
            line.Participants.Add(p1);
            line.Participants.Add(p2);
            line.Participants.Add(p3);
            context.Lines.Add(line);
            await context.SaveChangesAsync();
        }

        var repository = new SQLLineRepository(_fixture);

        // Act: Launch 5 concurrent TryAdvanceAsync calls
        var tasks = Enumerable.Range(0, 5)
            .Select(async _ =>
            {
                try
                {
                    return await repository.TryAdvanceAsync("CONC-001");
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Concurrency loss: another task already advanced
                    return false;
                }
                catch (DbException)
                {
                    // SQLite lock/busy error: treat as losing outcome
                    return false;
                }
            })
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert: Exactly one returned true, all others false
        var successCount = results.Count(r => r);
        Assert.Equal(1, successCount);

        // Verify the front participant moved by exactly one position
        using (var context = _fixture.CreateContext())
        {
            var advancedLine = await context.Lines
                .Include(l => l.Participants)
                .FirstAsync(l => l.Id == "CONC-001");

            var frontParticipant = advancedLine.Participants
                .Where(x => !x.Removed)
                .OrderBy(x => x.Position)
                .First();

            // p1 (Alice) should have been moved to the back (position 4), p2 (Bob) is now front
            Assert.Equal("Bob", frontParticipant.Name);
            var p1Updated = advancedLine.Participants.First(p => p.Name == "Alice");
            Assert.Equal(4, p1Updated.Position);
        }
    }

    /// <summary>
    /// Test 3: ConcurrencyToken_ChangesOnEveryUpdate
    /// Load a line, save a modification, and assert Version differs from its previous value.
    /// This pins MODEL-001's SaveChangesAsync override.
    /// </summary>
    [Fact]
    public async Task ConcurrencyToken_ChangesOnEveryUpdate()
    {
        // Arrange: Create a line
        var line = TestData.Line(id: "VER-001");
        var initialVersion = line.Version;

        using (var context = _fixture.CreateContext())
        {
            context.Lines.Add(line);
            await context.SaveChangesAsync();
        }

        // Act: Load and modify the line
        using (var context = _fixture.CreateContext())
        {
            var loadedLine = await context.Lines.FirstAsync(l => l.Id == "VER-001");
            var versionAfterLoad = loadedLine.Version;

            // Modify a property
            loadedLine.Name = "Modified Name";
            await context.SaveChangesAsync();

            var versionAfterModify = loadedLine.Version;

            // Assert: Version changed on every save
            Assert.NotEqual(initialVersion, versionAfterLoad);
            Assert.NotEqual(versionAfterLoad, versionAfterModify);
        }
    }

    /// <summary>
    /// Test 4: StaleWrite_Throws
    /// Load the same line into two separate contexts, save a change through the first,
    /// then save a different change through the second and assert DbUpdateConcurrencyException is thrown.
    /// This tests that the Version token is wired up correctly.
    /// </summary>
    [Fact]
    public async Task StaleWrite_Throws()
    {
        // Arrange: Create a line
        var line = TestData.Line(id: "STALE-001");

        using (var context = _fixture.CreateContext())
        {
            context.Lines.Add(line);
            await context.SaveChangesAsync();
        }

        // Load the same line into two separate contexts BEFORE either modifies
        ApplicationDbContext context1;
        ApplicationDbContext context2;
        Line loadedLine1;
        Line loadedLine2;

        context1 = _fixture.CreateContext();
        context2 = _fixture.CreateContext();

        try
        {
            loadedLine1 = await context1.Lines.FirstAsync(l => l.Id == "STALE-001");
            loadedLine2 = await context2.Lines.FirstAsync(l => l.Id == "STALE-001");

            // Both loaded with the same initial Version
            var initialVersion = loadedLine1.Version;
            Assert.Equal(initialVersion, loadedLine2.Version);

            // Context1 modifies and saves first (updates Version)
            loadedLine1.Name = "Modified by Context1";
            await context1.SaveChangesAsync();
            var versionAfterContext1Save = loadedLine1.Version;
            Assert.NotEqual(initialVersion, versionAfterContext1Save); // Version should have changed

            // Context2 modifies with the old Version still in memory
            loadedLine2.Name = "Modified by Context2";

            // Act & Assert: The second save should throw DbUpdateConcurrencyException
            // because Context2 is using the old Version token that no longer matches the database
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                async () => await context2.SaveChangesAsync()
            );
        }
        finally
        {
            await context1.DisposeAsync();
            await context2.DisposeAsync();
        }
    }

    /// <summary>
    /// Test 5: PausedLine_ConcurrentAdvance_NeverAdvances
    /// Pause a line whose deadline is past, fire 5 concurrent TryAdvanceAsync,
    /// assert every result is false and the queue is unchanged.
    /// </summary>
    [Fact]
    public async Task PausedLine_ConcurrentAdvance_NeverAdvances()
    {
        // Arrange: Create a paused line with a past deadline
        var line = TestData.Line(id: "PAUSE-001", autoAdvanceLine: true, autoReAdd: true);
        line.NextAdvanceAt = DateTime.UtcNow.AddSeconds(-1); // Past deadline
        line.IsPaused = true; // Paused
        line.NextPosition = 4; // Ensure NextPosition is beyond the initial 3 participants

        var p1 = TestData.Participant("Alice", position: 1);
        var p2 = TestData.Participant("Bob", position: 2);
        var p3 = TestData.Participant("Charlie", position: 3);

        using (var context = _fixture.CreateContext())
        {
            line.Participants.Add(p1);
            line.Participants.Add(p2);
            line.Participants.Add(p3);
            context.Lines.Add(line);
            await context.SaveChangesAsync();
        }

        var repository = new SQLLineRepository(_fixture);

        // Act: Launch 5 concurrent TryAdvanceAsync calls on a paused line
        var tasks = Enumerable.Range(0, 5)
            .Select(async _ =>
            {
                try
                {
                    return await repository.TryAdvanceAsync("PAUSE-001");
                }
                catch (DbUpdateConcurrencyException)
                {
                    return false;
                }
                catch (DbException)
                {
                    return false;
                }
            })
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert: All results are false
        Assert.All(results, r => Assert.False(r));

        // Verify the queue is unchanged: Alice is still first, all three still active
        using (var context = _fixture.CreateContext())
        {
            var unmodifiedLine = await context.Lines
                .Include(l => l.Participants)
                .FirstAsync(l => l.Id == "PAUSE-001");

            var frontParticipant = unmodifiedLine.Participants
                .Where(x => !x.Removed)
                .OrderBy(x => x.Position)
                .First();

            Assert.Equal("Alice", frontParticipant.Name);
            Assert.Equal(3, unmodifiedLine.Participants.Count(p => !p.Removed));
        }
    }

    /// <summary>
    /// Test 6: ManualAdvance_RacingBackgroundSweep_DoesNotThrow
    /// Fire a manual AdvanceLineAsync and a scheduled TryAdvanceAsync concurrently against the same
    /// due line. Neither call should throw. The line must end up in a consistent state: exactly 3
    /// active participants (AutoReAdd=true means nobody is permanently removed), all with distinct
    /// positions, and at least one TurnCount incremented (proving at least one advance actually applied).
    /// </summary>
    [Fact]
    public async Task ManualAdvance_RacingBackgroundSweep_DoesNotThrow()
    {
        // Arrange: a due line with three participants, auto-advance on, auto-reAdd on
        var line = TestData.Line(id: "RACE-001", autoAdvanceLine: true, autoReAdd: true, autoRestartTimerOnAdvance: false);
        line.NextAdvanceAt = DateTime.UtcNow.AddSeconds(-1); // Past deadline
        line.NextPosition = 4;

        var p1 = TestData.Participant("Alice", position: 1);
        var p2 = TestData.Participant("Bob", position: 2);
        var p3 = TestData.Participant("Charlie", position: 3);

        using (var context = _fixture.CreateContext())
        {
            line.Participants.Add(p1);
            line.Participants.Add(p2);
            line.Participants.Add(p3);
            context.Lines.Add(line);
            await context.SaveChangesAsync();
        }

        var repository = new SQLLineRepository(_fixture);

        // Act: race a manual advance against a scheduled advance. Neither call may throw.
        var manualTask = repository.AdvanceLineAsync("RACE-001");
        var scheduledTask = repository.TryAdvanceAsync("RACE-001");

        var exception = await Record.ExceptionAsync(async () => await Task.WhenAll(manualTask, scheduledTask));

        // Assert: no exception escaped either call
        Assert.Null(exception);

        // Assert: the line is in a consistent state afterward
        using (var context = _fixture.CreateContext())
        {
            var finalLine = await context.Lines
                .Include(l => l.Participants)
                .FirstAsync(l => l.Id == "RACE-001");

            var activeParticipants = finalLine.Participants.Where(p => !p.Removed).ToList();
            Assert.Equal(3, activeParticipants.Count);
            Assert.Equal(activeParticipants.Count, activeParticipants.Select(p => p.Position).Distinct().Count());
            Assert.True(finalLine.Participants.Sum(p => p.TurnCount) >= 1);
        }
    }

    /// <summary>
    /// Test 7: ConcurrentAddParticipant_AllSucceedWithUniquePositions
    /// Launch 5 concurrent AddParticipantAsync calls against a brand-new empty line.
    /// None may throw, and all 5 participants must end up with distinct, sequential positions.
    /// </summary>
    [Fact]
    public async Task ConcurrentAddParticipant_AllSucceedWithUniquePositions()
    {
        // Arrange: an empty line
        var line = TestData.Line(id: "JOIN-001");

        using (var context = _fixture.CreateContext())
        {
            context.Lines.Add(line);
            await context.SaveChangesAsync();
        }

        var repository = new SQLLineRepository(_fixture);

        // Act: 5 concurrent joins
        var tasks = Enumerable.Range(0, 5)
            .Select(i =>
            {
                var participant = new Participant
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"Joiner{i}",
                    Color = "#FF5733"
                };
                return repository.AddParticipantAsync("JOIN-001", participant);
            })
            .ToList();

        var exception = await Record.ExceptionAsync(async () => await Task.WhenAll(tasks));

        // Assert: no exception escaped any call
        Assert.Null(exception);

        // Assert: all 5 joiners got distinct, sequential positions
        using (var context = _fixture.CreateContext())
        {
            var finalLine = await context.Lines
                .Include(l => l.Participants)
                .FirstAsync(l => l.Id == "JOIN-001");

            Assert.Equal(5, finalLine.Participants.Count);
            var positions = finalLine.Participants.Select(p => p.Position).OrderBy(p => p).ToList();
            Assert.Equal(new List<int> { 1, 2, 3, 4, 5 }, positions);
        }
    }
}
