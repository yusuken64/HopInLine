namespace HopInLine.Data.Line
{
    public interface ILineRepository
    {
        void AddLine(Line line);
        Task AddLineAsync(Line newLine, CancellationToken cancellationToken);
		Task AdvanceLineAsync(string lineID);
        Task AddParticipantAsync(string lineID, Participant participant);
		Task DeleteParticipantAsync(string lineID, string instanceId);
        List<Line> GetAllLines();
        Task<List<Line>> GetAllLinesAsync(CancellationToken cancellationToken);
        Task<Line?> GetLineAsync(string id);
        bool LineExists(string newId);
        void RemoveLine(string id);
        Task RemoveLineAsync(string lineId, CancellationToken cancellationToken);
        Task UpdateLineAsync(Line line);
		Task ReAddRemovedParticipantAsync(string lineID, string particiantID);
		Task MoveParticipantUpAsync(string lineID, string participantID);
		Task MoveParticipantDownAsync(string lineID, string participantID);
		Task RemovedParticipantAsync(string lineID, string participantID);
	}
}