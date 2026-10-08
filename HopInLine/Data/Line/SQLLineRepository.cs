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

        private async Task ExecuteWithRetryAsync(Func<ApplicationDbContext, Task> action, int maxAttempts = 3)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                await using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    await action(context);
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return;
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync();
                    if (attempt == maxAttempts) throw;
                }
            }
        }

        private static void ApplyAdvanceTransition(Line line)
        {
            var participant = line.Participants
                .Where(x => !x.Removed)
                .OrderBy(x => x.Position)
                .FirstOrDefault();

            if (participant == null)
                return;

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

            line.NextAdvanceAt = line.AutoRestartTimerOnAdvance ? DateTime.UtcNow + line.AutoAdvanceInterval : null;
            line.IsPaused = !line.AutoRestartTimerOnAdvance;
            line.CountDownStart = DateTime.UtcNow;
        }

        public async Task AddLineAsync(Line newLine, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Lines.AddAsync(newLine, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task AdvanceLineAsync(string lineId)
        {
            await ExecuteWithRetryAsync(async context =>
            {
                var line = await context.Lines
                    .Include(l => l.Participants)
                    .FirstOrDefaultAsync(l => l.Id == lineId);

                if (line == null)
                    return;

                ApplyAdvanceTransition(line);
            });
        }


        public async Task ReAddRemovedParticipantAsync(string lineID, string particiantID)
        {
            await ExecuteWithRetryAsync(async context =>
            {
                var line = await context.Lines
                    .Include(l => l.Participants)
                    .FirstOrDefaultAsync(l => l.Id == lineID);

                if (line == null)
                    return;

                var participant = line.Participants.FirstOrDefault(x => x.Id == particiantID);
                if (participant == null || participant.Removed == false)
                    return;

                participant.Position = line.NextPosition++;
                participant.Removed = false;
            });
        }


        public async Task MoveParticipantUpAsync(string lineID, string participantID)
        {
            await ExecuteWithRetryAsync(async context =>
            {
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
                    return;

                int temp = participant.Position;
                participant.Position = participantAbove.Position;
                participantAbove.Position = temp;
            });
        }

        public async Task MoveParticipantDownAsync(string lineID, string participantID)
        {
            await ExecuteWithRetryAsync(async context =>
            {
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
                    return;

                int temp = participant.Position;
                participant.Position = participantBelow.Position;
                participantBelow.Position = temp;
            });
        }

        public async Task RemovedParticipantAsync(string lineID, string participantID)
        {
            await ExecuteWithRetryAsync(async context =>
            {
                var line = await context.Lines
                    .Include(l => l.Participants)
                    .FirstOrDefaultAsync(l => l.Id == lineID);

                if (line == null)
                    return;

                var participant = line.Participants.FirstOrDefault(p => p.Id == participantID && !p.Removed);
                if (participant == null)
                    return;

                participant.Removed = true;
            });
        }

        public async Task AddParticipantAsync(string lineID, Participant participant)
        {
            await ExecuteWithRetryAsync(async context =>
            {
                var line = await context.Lines
                    .Include(l => l.Participants)
                    .FirstOrDefaultAsync(l => l.Id == lineID);

                if (line == null)
                {
                    throw new ArgumentException($"Line with ID {lineID} not found.");
                }

                participant.Position = line.NextPosition++;
                participant.Removed = false;
                line.Participants.Add(participant);
            });
        }

        public async Task DeleteParticipantAsync(string lineID, string instanceId)
        {
            await ExecuteWithRetryAsync(async context =>
            {
                var participant = await context.Participants
                    .FirstOrDefaultAsync(p => p.Id == instanceId && p.LineId == lineID);

                if (participant != null)
                {
                    context.Participants.Remove(participant);
                }
            });
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

            await ExecuteWithRetryAsync(async context =>
            {
                var existingLine = await context.Lines
                    .FirstOrDefaultAsync(l => l.Id == line.Id);

                if (existingLine == null)
                    throw new InvalidOperationException("Line not found.");

                context.Entry(existingLine).CurrentValues.SetValues(line);
            });
        }

        public async Task PauseTimerAsync(string lineId)
        {
            await ExecuteWithRetryAsync(async context =>
            {
                var line = await context.Lines.FirstOrDefaultAsync(l => l.Id == lineId);
                if (line == null) return;

                if (line.NextAdvanceAt == null)
                    return;

                var remaining = line.NextAdvanceAt.Value - DateTime.UtcNow;
                if (remaining < TimeSpan.Zero)
                    remaining = TimeSpan.Zero;

                line.UnpauseRemaining = remaining;
                line.NextAdvanceAt = null;
                line.IsPaused = true;
            });
        }

        public async Task ResumeTimerAsync(string lineId)
        {
            await ExecuteWithRetryAsync(async context =>
            {
                var line = await context.Lines.FirstOrDefaultAsync(l => l.Id == lineId);
                if (line == null)
                    return;

                line.NextAdvanceAt = DateTime.UtcNow + (line.UnpauseRemaining ?? line.AutoAdvanceInterval);
                line.UnpauseRemaining = null;
                line.IsPaused = false;
                line.CountDownStart = DateTime.UtcNow;
            });
        }

        public async Task RestartTimerAsync(string lineId)
        {
            await ExecuteWithRetryAsync(async context =>
            {
                var line = await context.Lines.FirstOrDefaultAsync(l => l.Id == lineId);
                if (line == null) return;

                line.NextAdvanceAt = DateTime.UtcNow + line.AutoAdvanceInterval;
                line.UnpauseRemaining = null;
                line.IsPaused = false;
                line.CountDownStart = DateTime.UtcNow;
            });
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

        public async Task<int> DeleteLinesIdleSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            // Participants are removed by the database cascade on the LineId foreign key
            return await context.Lines
                .Where(l => l.LastUpdated < cutoffUtc)
                .ExecuteDeleteAsync(cancellationToken);
        }

        public async Task<bool> TryAdvanceAsync(string lineId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.PreserveLastUpdated = true; // auto-advance is not user activity
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

                // Check if there is a non-removed participant to advance
                var participant = line.Participants
                    .Where(x => !x.Removed)
                    .OrderBy(x => x.Position)
                    .FirstOrDefault();

                if (participant == null)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                ApplyAdvanceTransition(line);

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