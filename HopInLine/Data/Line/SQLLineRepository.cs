using Microsoft.EntityFrameworkCore;

namespace HopInLine.Data.Line
{
    public class SQLLineRepository : ILineRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public SQLLineRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task AddLineAsync(Line newLine, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Lines.AddAsync(newLine, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task AdvanceLineAsync(string lineId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var line = await context.Lines
                .Include(l => l.Participants)
                .FirstOrDefaultAsync(l => l.Id == lineId);

            if (line == null)
                return;

            var participant = line.Participants
                .Where(x => !x.Removed)
                .OrderBy(x => x.Position)
                .FirstOrDefault();

            if (participant == null)
            {
                return;
            }
            if (line.AutoReAdd)
            {
                participant.Position = line.NextPosition++;
                participant.Removed = false;
            }
            else
            {
                participant.Removed = true;
            }
            participant.TurnCount++;

            // Apply Advance transition
            line.NextAdvanceAt = line.AutoRestartTimerOnAdvance ? DateTime.UtcNow + line.AutoAdvanceInterval : null;
            line.IsPaused = !line.AutoRestartTimerOnAdvance;
            line.CountDownStart = DateTime.UtcNow;

            await context.SaveChangesAsync();
        }


        public async Task ReAddRemovedParticipantAsync(string lineID, string particiantID)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var line = await context.Lines
                .Include(l => l.Participants)
                .FirstOrDefaultAsync(l => l.Id == lineID);

            if (line == null)
                return;

            var participant = line.Participants
                .FirstOrDefault(x => x.Id == particiantID);

            if (participant == null || participant.Removed == false)
            {
                return;
            }

            participant.Position = line.NextPosition++;
            participant.Removed = false;

            await context.SaveChangesAsync();
        }


        public async Task MoveParticipantUpAsync(string lineID, string participantID)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var line = await context.Lines
                .Include(l => l.Participants)
                .FirstOrDefaultAsync(l => l.Id == lineID);

            if (line == null)
                return;

            var participant = line.Participants.FirstOrDefault(p => p.Id == participantID && !p.Removed);
            if (participant == null)
                return;

            var participantAbove = line.Participants
                .Where(p => !p.Removed && p.Position < participant.Position)
                .OrderByDescending(p => p.Position)
                .FirstOrDefault();

            if (participantAbove == null)
                return; // Already at top

            // Swap positions
            int temp = participant.Position;
            participant.Position = participantAbove.Position;
            participantAbove.Position = temp;

            await context.SaveChangesAsync();
        }

        public async Task MoveParticipantDownAsync(string lineID, string participantID)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var line = await context.Lines
                .Include(l => l.Participants)
                .FirstOrDefaultAsync(l => l.Id == lineID);

            if (line == null)
                return;

            var participant = line.Participants.FirstOrDefault(p => p.Id == participantID && !p.Removed);
            if (participant == null)
                return;

            var participantBelow = line.Participants
                .Where(p => !p.Removed && p.Position > participant.Position)
                .OrderBy(p => p.Position)
                .FirstOrDefault();

            if (participantBelow == null)
                return; // Already at top

            // Swap positions
            int temp = participant.Position;
            participant.Position = participantBelow.Position;
            participantBelow.Position = temp;

            await context.SaveChangesAsync();
        }

        public async Task RemovedParticipantAsync(string lineID, string participantID)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var line = await context.Lines
                .Include(l => l.Participants)
                .FirstOrDefaultAsync(l => l.Id == lineID);

            if (line == null)
                return;

            var participant = line.Participants.FirstOrDefault(p => p.Id == participantID && !p.Removed);
            if (participant == null)
                return;

            participant.Removed = true;

            await context.SaveChangesAsync();
        }

        public async Task AddParticipantAsync(string lineID, Participant participant)
        {
            // Retrieve the line by its ID
            await using var context = await _contextFactory.CreateDbContextAsync();

            var line = await context.Lines
                .Include(l => l.Participants) // Ensure participants are included
                .FirstOrDefaultAsync(l => l.Id == lineID);

            if (line != null)
            {
                // Set the participant's position using NextPosition
                participant.Position = line.NextPosition++;

                // Set the participant as not removed
                participant.Removed = false;

                // Add the new participant to the line's participants list
                line.Participants.Add(participant);

                // Save changes to the database
                await context.SaveChangesAsync();
            }
            else
            {
                // Handle the case where the line was not found
                throw new ArgumentException($"Line with ID {lineID} not found.");
            }
        }

        public async Task DeleteParticipantAsync(string lineID, string instanceId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var participant = await context.Participants
                .FirstOrDefaultAsync(p => p.Id == instanceId && p.LineId == lineID);

            if (participant != null)
            {
                context.Participants.Remove(participant);
                await context.SaveChangesAsync();
            }
        }

        public async Task<List<Line>> GetAllLinesAsync(CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Lines
                .Include(l => l.Participants)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<Line?> GetLineAsync(string id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Lines
                .Include(l => l.Participants)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public bool LineExists(string id)
        {
            using var context = _contextFactory.CreateDbContext();
            return context.Lines
                .AsNoTracking()
                .Any(line => line.Id == id);
        }

        public async Task RemoveLineAsync(string lineId, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var line = await context.Lines.FindAsync(new object[] { lineId }, cancellationToken);
            if (line != null)
            {
                context.Lines.Remove(line);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        //This method does not change the participants.
        public async Task UpdateLineAsync(Line line)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existingLine = await context.Lines
                .FirstOrDefaultAsync(l => l.Id == line.Id);

            if (existingLine == null)
                throw new InvalidOperationException("Line not found.");

            context.Entry(existingLine).CurrentValues.SetValues(line);
            await context.SaveChangesAsync();
        }

        public async Task PauseTimerAsync(string lineId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var line = await context.Lines.FirstOrDefaultAsync(l => l.Id == lineId);
            if (line == null) return;

            // Pause transition: if NextAdvanceAt is null, do nothing
            if (line.NextAdvanceAt == null)
                return;

            // Set UnpauseRemaining = NextAdvanceAt.Value - DateTime.UtcNow, clamped to TimeSpan.Zero if negative
            var remaining = line.NextAdvanceAt.Value - DateTime.UtcNow;
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;

            line.UnpauseRemaining = remaining;
            line.NextAdvanceAt = null;
            line.IsPaused = true;

            await context.SaveChangesAsync();
        }

        public async Task ResumeTimerAsync(string lineId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var line = await context.Lines.FirstOrDefaultAsync(l => l.Id == lineId);
            if (line == null)
                return;

            // Resume transition: set NextAdvanceAt = DateTime.UtcNow + (UnpauseRemaining ?? AutoAdvanceInterval)
            line.NextAdvanceAt = DateTime.UtcNow + (line.UnpauseRemaining ?? line.AutoAdvanceInterval);
            line.UnpauseRemaining = null;
            line.IsPaused = false;
            line.CountDownStart = DateTime.UtcNow;

            await context.SaveChangesAsync();
        }

        public async Task RestartTimerAsync(string lineId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var line = await context.Lines.FirstOrDefaultAsync(l => l.Id == lineId);
            if (line == null) return;

            // Restart transition: set NextAdvanceAt = DateTime.UtcNow + AutoAdvanceInterval
            line.NextAdvanceAt = DateTime.UtcNow + line.AutoAdvanceInterval;
            line.UnpauseRemaining = null;
            line.IsPaused = false;
            line.CountDownStart = DateTime.UtcNow;

            await context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<string>> GetDueLineIdsAsync(DateTime utcNow, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Lines
                .AsNoTracking()
                .Where(l => l.NextAdvanceAt != null
                         && l.NextAdvanceAt <= utcNow
                         && l.AutoAdvanceLine
                         && !l.IsPaused
                         && l.Participants.Any(p => !p.Removed))
                .Select(l => l.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> TryAdvanceAsync(string lineId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // Load the line tracked, with participants
                var line = await context.Lines
                    .Include(l => l.Participants)
                    .FirstOrDefaultAsync(l => l.Id == lineId);

                // Re-check the deadline inside the transaction
                if (line == null
                    || line.NextAdvanceAt == null
                    || line.NextAdvanceAt > DateTime.UtcNow
                    || line.IsPaused
                    || !line.AutoAdvanceLine)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                // Perform the same advance logic as AdvanceLineAsync
                var participant = line.Participants
                    .Where(x => !x.Removed)
                    .OrderBy(x => x.Position)
                    .FirstOrDefault();

                if (participant == null)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                if (line.AutoReAdd)
                {
                    participant.Position = line.NextPosition++;
                    participant.Removed = false;
                }
                else
                {
                    participant.Removed = true;
                }
                participant.TurnCount++;

                // Apply Advance transition
                line.NextAdvanceAt = line.AutoRestartTimerOnAdvance ? DateTime.UtcNow + line.AutoAdvanceInterval : null;
                line.IsPaused = !line.AutoRestartTimerOnAdvance;
                line.CountDownStart = DateTime.UtcNow;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return false;
            }
        }
    }
}