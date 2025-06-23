using System.ComponentModel.DataAnnotations.Schema;

namespace HopInLine.Data.Line
{
	public class Line
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public string? Description { get; set; }
		public DateTime LastUpdated { get; set; }
		public bool AutoAdvanceLine { get; set; }
		public TimeSpan AutoAdvanceInterval { get; set; }
		public DateTime CountDownStart { get; set; }
		public bool AutoReAdd { get; set; }
		public List<Participant> Participants { get; set; } = new();
		public int NextPosition { get; set; } = 1;
	}

	public class LineDto
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public List<ParticipantDto> Participants { get; set; }
		public List<ParticipantDto> RemovedParticipants { get; set; }
		public string? Description { get; set; }
		public DateTime LastUpdated { get; set; }
		public bool AutoAdvanceLine { get; set; }
		public TimeSpan AutoAdvanceInterval { get; set; }
		public DateTime CountDownStart { get; set; }
		public bool AutoReAdd { get; set; }

		internal static LineDto FromLine(Line line)
		{
			return new LineDto()
			{
				Id = line.Id,
				Name = line.Name,
				AutoAdvanceInterval = line.AutoAdvanceInterval,
				AutoAdvanceLine = line.AutoAdvanceLine,
				CountDownStart = line.CountDownStart,
				AutoReAdd = line.AutoReAdd,
				Description = line.Description,
				LastUpdated = DateTime.Now,
				Participants = line.Participants
					.Where(x => !x.Removed)
					.Select(ParticipantDto.FromParticipant)
					.ToList(),
				RemovedParticipants = line.Participants
					.Where(x => x.Removed)
					.Select(ParticipantDto.FromParticipant)
					.ToList()
			};
		}
	}


	public class ParticipantDto
	{
		public string Id { get; set; }
		public string Name { get; set; }		
		public string? Description { get; set; }
		public string Color { get; set; }
		public int TurnCount { get; set; }
		public int Position { get; set; }
		public bool Removed { get; set; } = false;

		internal static ParticipantDto FromParticipant(Participant p)
		{
			return new ParticipantDto()
			{
				Id = p.Id,
				Name = p.Name,
				Description = p.Description,
				Color = p.Color,
				Position = p.Position,
				Removed = p.Removed,
				TurnCount = p.TurnCount
			};
		}
	}
}
