namespace HopInLine.Data.Line
{
    public class Line
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public List<Participant> Participants { get; set; } = new List<Participant>();
        public List<Participant> RemovedParticipants { get; set; } = new List<Participant>();
        public DateTime LastUpdated { get; set; }
        public bool AutoAdvanceLine { get; set; }
        public TimeSpan AutoAdvanceInterval { get; set; }
        public DateTime CountDownStart { get; set; }
        public bool AutoReAdd { get; set; }
    }
}
