using Microsoft.EntityFrameworkCore;

namespace HopInLine.Data.Line
{
    public class SQLLineRepository : ILineRepository
    {
        private readonly ApplicationDbContext _context;

        public SQLLineRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public void AddLine(Line line)
        {
            _context.Lines.Add(line);
            _context.SaveChanges();
        }

        public async Task AddLineAsync(Line newLine, CancellationToken cancellationToken)
        {
            await _context.Lines.AddAsync(newLine, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task AdvanceLineAsync(string lineId)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            var line = await _context.Lines
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

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }


        public async Task ReAddRemovedParticipantAsync(string lineID, string particiantID)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            var line = await _context.Lines
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

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }


        public async Task MoveParticipantUpAsync(string lineID, string participantID)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            var line = await _context.Lines
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

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        public async Task MoveParticipantDownAsync(string lineID, string participantID)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            var line = await _context.Lines
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

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        public async Task RemovedParticipantAsync(string lineID, string participantID)
        {
            var line = await _context.Lines
                .Include(l => l.Participants)
                .FirstOrDefaultAsync(l => l.Id == lineID);

            if (line == null)
                return;

            var participant = line.Participants.FirstOrDefault(p => p.Id == participantID && !p.Removed);
            if (participant == null)
                return;

            participant.Removed = true;

            await _context.SaveChangesAsync();
        }

        public async Task AddParticipantAsync(string lineID, Participant participant)
        {
            // Retrieve the line by its ID
            var line = await _context.Lines
                .Include(l => l.Participants) // Ensure participants are included
                .FirstOrDefaultAsync(l => l.Id == lineID);

            if (line != null)
            {
                // Set the participant's position to the highest in the line
                if (line.Participants.Any())
                {
                    participant.Position = line.Participants.Max(p => p.Position) + 1;
                }
                else
                {
                    participant.Position = 0; // If there are no participants, start at position 0
                }

                // Set the participant as not removed
                participant.Removed = false;

                // Add the new participant to the line's participants list
                line.Participants.Add(participant);

                // Save changes to the database
                _context.SaveChanges();
            }
            else
            {
                // Handle the case where the line was not found
                throw new ArgumentException($"Line with ID {lineID} not found.");
            }
        }

        public async Task DeleteParticipantAsync(string lineID, string instanceId)
        {
            var participant = await _context.Participants
                .FirstOrDefaultAsync(p => p.Id == instanceId && p.LineId == lineID);

            if (participant != null)
            {
                _context.Participants.Remove(participant);
                await _context.SaveChangesAsync();
            }
        }

        public List<Line> GetAllLines()
        {
            return _context.Lines.Include(l => l.Participants).ToList();
        }

        public async Task<List<Line>> GetAllLinesAsync(CancellationToken cancellationToken)
        {
            return await _context.Lines
                .Include(l => l.Participants)
                .ToListAsync(cancellationToken);
        }

        public async Task<Line?> GetLineAsync(string id)
        {
            return await _context.Lines
                .Include(l => l.Participants)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public bool LineExists(string id)
        {
            return _context.Lines.Any(line => line.Id == id);
        }

        public void RemoveLine(string id)
        {
            // Retrieve the line by its ID
            var line = _context.Lines
                .Include(l => l.Participants) // Include participants to ensure cascading deletion if necessary
                .FirstOrDefault(l => l.Id == id);

            if (line != null)
            {
                // Remove the line from the context
                _context.Lines.Remove(line);

                // Save changes to the database
                _context.SaveChanges();
            }
        }

        public async Task RemoveLineAsync(string lineId, CancellationToken cancellationToken)
        {
            var line = await _context.Lines.FindAsync(new object[] { lineId }, cancellationToken);
            if (line != null)
            {
                _context.Lines.Remove(line);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        //This method does not change the participants.
        public async Task UpdateLineAsync(Line line)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));

            var existingLine = await _context.Lines
                .FirstOrDefaultAsync(l => l.Id == line.Id);

            if (existingLine == null)
                throw new InvalidOperationException("Line not found.");

            _context.Entry(existingLine).CurrentValues.SetValues(line);
            await _context.SaveChangesAsync();
        }

        public async Task PauseTimerAsync(string lineId)
        {
            var line = await _context.Lines.FirstOrDefaultAsync(l => l.Id == lineId);
            if (line == null) return;

            var elapsed = DateTime.UtcNow - line.CountDownStart;
            var remaining = line.AutoAdvanceInterval - elapsed;

            // Cap at zero
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;

            line.IsPaused = true;
            line.UnpauseRemaining = remaining;

            await _context.SaveChangesAsync();
        }

        public async Task ResumeTimerAsync(string lineId)
        {
            var line = await _context.Lines.FirstOrDefaultAsync(l => l.Id == lineId);
            if (line == null || !line.IsPaused || !line.UnpauseRemaining.HasValue)
                return;

            line.CountDownStart = DateTime.UtcNow - (line.AutoAdvanceInterval - line.UnpauseRemaining.Value);
            line.IsPaused = false;
            line.UnpauseRemaining = null;

            await _context.SaveChangesAsync();
        }

        public async Task RestartTimerAsync(string lineId)
        {
            var line = await _context.Lines.FirstOrDefaultAsync(l => l.Id == lineId);
            if (line == null) return;

            line.CountDownStart = DateTime.UtcNow;
            line.IsPaused = false;
            line.UnpauseRemaining = null;

            await _context.SaveChangesAsync();
        }
    }
}