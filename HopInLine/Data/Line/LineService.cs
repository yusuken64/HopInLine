using HopInLine.Pages.Line;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HopInLine.Data.Line
{
    public class LineService
    {
        private readonly ILineRepository _lineRepository;
        private readonly LineUpdatedNotifier lineUpdatedNotifier;
        private readonly ParticipantFactory _participantFactory = new();

        public LineService(ILineRepository lineRepository,
            LineUpdatedNotifier lineUpdatedNotifier)
        {
            _lineRepository = lineRepository;
            this.lineUpdatedNotifier = lineUpdatedNotifier;
        }
        public async Task<Line> CreateLineAsync(
            string name,
            string description,
            bool audoReAdd,
            bool autoAdvanceLine,
            TimeSpan autoAdvanceInterval)
        {
            const int maxAttempts = 100;
            int attempt = 0;
            string newId;

            do
            {
                newId = IdGenerator.GenerateUniqueId();
                attempt++;
            }
            while (_lineRepository.LineExists(newId) && attempt < maxAttempts);

            if (string.IsNullOrWhiteSpace(newId)) { return null; }

            var newLine = new Line
            {
                Id = newId,
                Name = name,
                Description = description,
                AutoAdvanceLine = autoAdvanceLine,
                AutoAdvanceInterval = autoAdvanceInterval,
                AutoReAdd = audoReAdd,
                CountDownStart = DateTime.UtcNow
            };

            RestartTimer(newLine);
            await AddLineAsync(newLine, new());
            return await Task.FromResult(newLine);
        }

		public async Task<Line> UpdateLineAsync(Line line)
		{
			RestartTimer(line);
			await _lineRepository.UpdateLineAsync(line);
            await lineUpdatedNotifier.NotifyLineUpdatedAsync(new LineChangedEventArgs(line));
			return line;
		}

		public async Task<Line> GetLineByIdAsync(string id)
        {
            return await _lineRepository.GetLineAsync(id);
        }

        public async Task<List<Line>> GetAllLinesAsync()
        {
            var lines = _lineRepository.GetAllLines();
            return await Task.FromResult(lines);
        }

        public async Task DeleteLineAsync(string id)
        {
            var line = _lineRepository.GetLineAsync(id);
            if (line != null)
            {
                _lineRepository.RemoveLine(id);
            }
            lineUpdatedNotifier.StopLineAdvancement(id);

            await Task.CompletedTask;
        }

        public async Task AddParticipantAsync(string lineID, Participant participant)
        {
            await _lineRepository.AddParticipantAsync(lineID, participant);
            var line = await _lineRepository.GetLineAsync(lineID);
            if (line.Participants.Count == 1) { line.CountDownStart = DateTime.UtcNow; }
            await lineUpdatedNotifier.NotifyLineUpdatedAsync(new LineChangedEventArgs(line));
        }

        public async Task AdvanceLineAsync(string lineID)
        {
            await _lineRepository.AdvanceLineAsync(lineID);
			var line = await _lineRepository.GetLineAsync(lineID);
            if (line == null ||
                line.Participants.IsNullOrEmpty())
            {
                return;
            }
            RestartTimer(line);
            await lineUpdatedNotifier.NotifyLineUpdatedAsync(new LineChangedEventArgs(line));
        }

        private void RestartTimer(Line line)
        {
            lineUpdatedNotifier.StopLineAdvancement(line.Id);

            if (line == null) { return; }

            if (line.AutoAdvanceLine &&
                line.AutoAdvanceInterval >= TimeSpan.FromMinutes(0) &&
                line.Participants.Any())
            {
                line.CountDownStart = DateTime.UtcNow;
                lineUpdatedNotifier.StartLineAdvancement(line);
            }
        }

        internal async Task MoveParticipantUpAsync(string lineID, string participantID)
        {
            var line = await _lineRepository.GetLineAsync(lineID);
            if (line == null) { return; }
            var originalFirstParticipant = line.Participants.OrderBy(x => x.Position).FirstOrDefault();

            await _lineRepository.MoveParticipantUpAsync(lineID, participantID);
            line = await _lineRepository.GetLineAsync(lineID);
            var participant = line.Participants.OrderBy(x => x.Position).FirstOrDefault();

            if (participant?.Id != originalFirstParticipant?.Id)
            {
                RestartTimer(line);
            }
            await lineUpdatedNotifier.NotifyLineUpdatedAsync(new LineChangedEventArgs(line));
        }

        internal async Task MoveParticipantDownAsync(string lineID, string participantID)
        {
            var line = await _lineRepository.GetLineAsync(lineID);
            if (line == null) { return; }
            var originalFirstParticipant = line.Participants.OrderBy(x => x.Position).FirstOrDefault();

            await _lineRepository.MoveParticipantDownAsync(lineID, participantID);
            line = await _lineRepository.GetLineAsync(lineID);
            var participant = line.Participants.OrderBy(x => x.Position).FirstOrDefault();

            if (participant?.Id != originalFirstParticipant?.Id)
            {
                RestartTimer(line);
            }
            await lineUpdatedNotifier.NotifyLineUpdatedAsync(new LineChangedEventArgs(line));
        }

        internal async Task RemoveParticipantAsync(string lineID, string participantID)
        {
            var line = await _lineRepository.GetLineAsync(lineID);
            if (line == null) { return; }
            var first = line.Participants
                .Where(x => !x.Removed)
                .OrderBy(x => x.Position).FirstOrDefault();

            await _lineRepository.RemovedParticipantAsync(lineID, participantID);
            line = await _lineRepository.GetLineAsync(lineID);

            if (first?.Id == participantID)
            {
                RestartTimer(line);
            }
            await lineUpdatedNotifier.NotifyLineUpdatedAsync(new LineChangedEventArgs(line));
        }

        internal async Task ReAddRemovedParticipantAsync(string lineID, string participantID)
        {
            var line = await _lineRepository.GetLineAsync(lineID);
            if (line == null) { return; }
            var removedParticipant = line.Participants.FirstOrDefault(x => x.Id.Equals(participantID));
            if (removedParticipant == null) { return; }

            await _lineRepository.ReAddRemovedParticipantAsync(lineID, participantID);
            line = await _lineRepository.GetLineAsync(lineID);

            await lineUpdatedNotifier.NotifyLineUpdatedAsync(new LineChangedEventArgs(line));
        }

        internal async Task DeleteRemovedParticipantAsync(string lineID, string instanceId)
        {
            await _lineRepository.DeleteParticipantAsync(lineID, instanceId);
            var line = await _lineRepository.GetLineAsync(lineID);
            await lineUpdatedNotifier.NotifyLineUpdatedAsync(new LineChangedEventArgs(line));
        }

        public async Task AddLineAsync(Line newLine, CancellationToken cancellationToken)
        {
            await _lineRepository.AddLineAsync(newLine, cancellationToken);
            await lineUpdatedNotifier.NotifyLineUpdatedAsync(new LineChangedEventArgs(newLine));
        }

        public async Task RemoveLineAsync(string lineId, CancellationToken cancellationToken)
        {
            lineUpdatedNotifier.StopLineAdvancement(lineId);
            await _lineRepository.RemoveLineAsync(lineId, cancellationToken);
        }

		public async Task StartTimerAsync(string lineId)
		{
            var line = await GetLineByIdAsync(lineId);
			lineUpdatedNotifier.StartLineAdvancement(line);
			await lineUpdatedNotifier.NotifyLineUpdatedAsync(new LineChangedEventArgs(line));
		}

		public async Task StopTimerAsync(string lineId)
		{
			var line = await GetLineByIdAsync(lineId);
            line.AutoAdvanceLine = false;
			lineUpdatedNotifier.StopLineAdvancement(lineId);
			await lineUpdatedNotifier.NotifyLineUpdatedAsync(new LineChangedEventArgs(line));
		}

        public static LineViewModel ToLiveViewModel(Line line)
        {
            return new()
            {
                AutoAdvanceInterval = line.AutoAdvanceInterval,
                AutoAdvanceLine = line.AutoAdvanceLine,
                AutoReAdd = line.AutoReAdd,
                CountDownStart = line.CountDownStart,
                Description = line.Description,
                Id = line.Id,
                LastUpdated = line.LastUpdated,
                Name = line.Name,
                Participants = line.Participants.Where(x => !x.Removed).ToList(),
                RemovedParticipants = line.Participants.Where(x => x.Removed).ToList()
            };
        }
	}
}