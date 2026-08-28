namespace HopInLine.Data.Line
{
    public interface ILineRepository
    {
        Task AddLineAsync(Line newLine, CancellationToken cancellationToken);
		Task AdvanceLineAsync(string lineID);
        Task AddParticipantAsync(string lineID, Participant participant);
		Task DeleteParticipantAsync(string lineID, string instanceId);
        Task<Line?> GetLineAsync(string id);
        bool LineExists(string newId);
        Task RemoveLineAsync(string lineId, CancellationToken cancellationToken);
        Task UpdateLineAsync(Line line);
		Task ReAddRemovedParticipantAsync(string lineID, string particiantID);
		Task MoveParticipantUpAsync(string lineID, string participantID);
		Task MoveParticipantDownAsync(string lineID, string participantID);
		Task RemovedParticipantAsync(string lineID, string participantID);
		Task ResumeTimerAsync(string lineId);
		Task PauseTimerAsync(string lineId);
		Task RestartTimerAsync(string lineId);
		Task<IReadOnlyList<string>> GetDueLineIdsAsync(DateTime utcNow, CancellationToken cancellationToken);
		Task<bool> TryAdvanceAsync(string lineId);
	}
}