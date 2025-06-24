using System.Collections.Concurrent;

namespace HopInLine.Data.Line
{
    public class InMemoryLineRepository : ILineRepository
    {
        private readonly ConcurrentDictionary<string, Line> _lines;

        public InMemoryLineRepository()
        {
            _lines = new ConcurrentDictionary<string, Line>();
        }

        public void AddLine(Line line)
        {
            if (!_lines.TryAdd(line.Id, line))
            {
                throw new InvalidOperationException($"A line with ID {line.Id} already exists.");
            }
        }

        public async Task AddLineAsync(Line newLine, CancellationToken cancellationToken)
        {
            await Task.Run(() => AddLine(newLine), cancellationToken);
        }

        public async Task AddParticipantAsync(string lineID, Participant participant)
        {
            if (_lines.TryGetValue(lineID, out var line))
            {
                line.Participants.Add(participant);
                await Task.CompletedTask;
            }
            else
            {
                throw new KeyNotFoundException($"Line with ID {lineID} not found.");
            }
        }

		public Task AdvanceLineAsync(string lineID)
		{
			throw new NotImplementedException();
		}

		public async Task DeleteParticipantAsync(string lineID, string instanceId)
        {
            if (_lines.TryGetValue(lineID, out var line))
            {
                var participant = line.Participants.FirstOrDefault(p => p.Id == instanceId);
                if (participant != null)
                {
                    line.Participants.Remove(participant);
                }
                await Task.CompletedTask;
            }
            else
            {
                throw new KeyNotFoundException($"Line with ID {lineID} not found.");
            }
        }

        public List<Line> GetAllLines()
        {
            return _lines.Values.ToList();
        }

        public async Task<List<Line>> GetAllLinesAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(() => GetAllLines(), cancellationToken);
        }

        public async Task<Line> GetLineAsync(string id)
        {
            if (_lines.TryGetValue(id, out var line))
            {
                return await Task.FromResult(line);
            }
            throw new KeyNotFoundException($"Line with ID {id} not found.");
        }

        public bool LineExists(string newId)
        {
            return _lines.ContainsKey(newId);
        }

		public Task MoveParticipantDownAsync(string lineID, string participantID)
		{
			throw new NotImplementedException();
		}

		public Task MoveParticipantUpAsync(string lineID, string participantID)
		{
			throw new NotImplementedException();
		}

		public Task PauseTimerAsync(string lineId)
		{
			throw new NotImplementedException();
		}

		public Task ReAddRemovedParticipantAsync(string lineID, string particiantID)
		{
			throw new NotImplementedException();
		}

		public Task RemovedParticipantAsync(string lineID, string participantID)
		{
			throw new NotImplementedException();
		}

		public void RemoveLine(string id)
        {
            if (!_lines.TryRemove(id, out _))
            {
                throw new KeyNotFoundException($"Line with ID {id} not found.");
            }
        }

        public async Task RemoveLineAsync(string lineId, CancellationToken cancellationToken)
        {
            await Task.Run(() => RemoveLine(lineId), cancellationToken);
        }

		public Task RestartTimerAsync(string lineId)
		{
			throw new NotImplementedException();
		}

		public Task ResumeTimerAsync(string lineId)
		{
			throw new NotImplementedException();
		}

		public async Task UpdateLineAsync(Line line)
        {
            if (_lines.ContainsKey(line.Id))
            {
                _lines[line.Id] = line;
                await Task.CompletedTask;
            }
            else
            {
                throw new KeyNotFoundException($"Line with ID {line.Id} not found.");
            }
        }
    }

}