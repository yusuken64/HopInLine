namespace HopInLine.Data.Line
{
    public interface ILineRepository
    {
        void AddLine(Line line);
        Task AddLineAsync(Line newLine, CancellationToken cancellationToken);
        Task AddParticipantAsync(string lineID, Participant participant);
        Task DeleteParticipantAsync(string lineID, string instanceId);
        List<Line> GetAllLines();
        Task<List<Line>> GetAllLinesAsync(CancellationToken cancellationToken);
        Task<Line> GetLineAsync(string id);
        bool LineExists(string newId);
        void RemoveLine(string id);
        Task RemoveLineAsync(string lineId, CancellationToken cancellationToken);
        Task UpdateLineAsync(Line line);
    }
}