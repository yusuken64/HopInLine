namespace HopInLine.Data.Line
{
	public class Participant
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public string? Description { get; set; }
		public string Color { get; set; }
		public int TurnCount { get; set; }
		public int Position { get; set; }
		public bool Removed { get; set; } = false;

		public string LineId { get; set; }  // foreign key to Line
		public Line Line { get; set; }      // navigation property
	}
}
