using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace HopInLine.Data.Line
{
    public class SQLLiteLineRepository : ILineRepository
    {
        private readonly ApplicationDbContext _context;

        public SQLLiteLineRepository(ApplicationDbContext context)
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
            var participant = await _context.Participants.FirstOrDefaultAsync(x => x.Id == instanceId);
            if (participant != null)
            {
                _context.Participants.Remove(participant);
                _context.SaveChanges();
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
        public async Task<Line> GetLineAsync(string id)
        {
            var line = await _context.Lines
                .FirstOrDefaultAsync(l => l.Id == id);

            if (line != null)
            {
                await _context.Entry(line)
                    .Collection(l => l.Participants)
                    .LoadAsync();
            }

            if (line != null)
            {
                // Separate participants into active and removed lists
                var activeParticipants = line.Participants
                    .Where(p => !p.Removed)
                    .OrderBy(p => p.Position)
                    .ToList();

                var removedParticipants = line.Participants
                    .Where(p => p.Removed)
                    .OrderBy(p => p.Position)
                    .ToList();

                // Assign the separated lists back to the line
                line.Participants = activeParticipants;
                line.RemovedParticipants = removedParticipants;
            }

            return line;
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

        public async Task UpdateLineAsync(Line line)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));

            // Retrieve the existing line from the database
            var existingLine = await _context.Lines
                .AsNoTracking()
                .Include(l => l.Participants)
                .Include(l => l.RemovedParticipants)
                .FirstOrDefaultAsync(l => l.Id == line.Id);

            if (existingLine == null)
                throw new InvalidOperationException("Line not found.");

            // Update the line's properties
            existingLine.Name = line.Name;
            existingLine.Description = line.Description;
            existingLine.AutoAdvanceLine = line.AutoAdvanceLine;
            existingLine.AutoAdvanceInterval = line.AutoAdvanceInterval;
            existingLine.CountDownStart = line.CountDownStart;
            existingLine.AutoReAdd = line.AutoReAdd;

            // Handle Participants
            var participantIds = line.Participants.Select(p => p.Id).ToList();
            var existingParticipants = await _context.Participants
                .Where(p => participantIds.Contains(p.Id))
                .AsNoTracking()
                .ToListAsync();

            for (int i = 0; i < line.Participants.Count; i++)
            {
                var participant = line.Participants[i];
                participant.Position = i;
                participant.Removed = false;

                var existingParticipant = existingParticipants
                    .FirstOrDefault(p => p.Id == participant.Id);

                if (existingParticipant != null)
                {
                    _context.Entry(existingParticipant).State = EntityState.Detached;
                    _context.Entry(participant).State = EntityState.Modified;
                }
                else
                {
                    _context.Participants.Add(participant);
                }
            }

            // Handle RemovedParticipants
            var removedParticipantIds = line.RemovedParticipants.Select(p => p.Id).ToList();
            var existingRemovedParticipants = await _context.Participants
                .Where(p => removedParticipantIds.Contains(p.Id))
                .AsNoTracking()
                .ToListAsync();

            for (int i = 0; i < line.RemovedParticipants.Count; i++)
            {
                var removedParticipant = line.RemovedParticipants[i];
                removedParticipant.Position = i;
                removedParticipant.Removed = true;

                var existingRemovedParticipant = existingRemovedParticipants
                    .FirstOrDefault(p => p.Id == removedParticipant.Id);

                if (existingRemovedParticipant != null)
                {
                    _context.Entry(existingRemovedParticipant).State = EntityState.Detached;
                    _context.Entry(removedParticipant).State = EntityState.Modified;
                }
                else
                {
                    _context.Participants.Add(removedParticipant);
                }
            }

            // Save changes to the database
            await _context.SaveChangesAsync();
        }
    }
}