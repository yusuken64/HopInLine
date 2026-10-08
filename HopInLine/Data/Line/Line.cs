using System.ComponentModel.DataAnnotations.Schema;

namespace HopInLine.Data.Line
{
	public class Line
	{
		public string Id { get; set; } = "";
		public string Name { get; set; } = "";
		public string? Description { get; set; }
		public DateTime LastUpdated { get; set; }

		//Timer Related
		public bool AutoAdvanceLine { get; set; }
		public TimeSpan AutoAdvanceInterval { get; set; }
		public DateTime CountDownStart { get; set; }
		public bool IsPaused {get;set;}
		public bool AutoReAdd { get; set; }
		public bool AutoRestartTimerOnAdvance { get; set; } = true;
		public TimeSpan? UnpauseRemaining { get; set; }
		/// <summary>Gap between an auto-advance and the start of the next person's interval. Zero means none.</summary>
		public TimeSpan GraceTime { get; set; }
		/// <summary>Lead-in before the interval runs when a manager resumes or restarts the timer. Zero means none.</summary>
		public TimeSpan StartCountdown { get; set; }
		/// <summary>When the current grace or start-countdown lead-in ends; null when none is running.</summary>
		public DateTime? DelayEndsAt { get; set; }
		/// <summary>True when the running lead-in is a start countdown, false when it is grace time.</summary>
		public bool DelayIsStartCountdown { get; set; }

		//Advance Management
		public DateTime? NextAdvanceAt { get; set; }
		public string ManageToken { get; set; } = string.Empty;
		public Guid Version { get; set; }

		//Participants
		public List<Participant> Participants { get; set; } = new();
		public int NextPosition { get; set; } = 1;
	}

	public class LineDto
	{
		public string Id { get; set; } = "";
		public string Name { get; set; } = "";
		public List<ParticipantDto> Participants { get; set; } = [];
		public List<ParticipantDto> RemovedParticipants { get; set; } = [];
		public string? Description { get; set; }
		public DateTime LastUpdated { get; set; }
		public bool AutoAdvanceLine { get; set; }
		public TimeSpan AutoAdvanceInterval { get; set; }
		public DateTime CountDownStart { get; set; }
		public bool AutoReAdd { get; set; }
		public bool IsPaused { get; set; }
		public bool AutoRestartTimerOnAdvance { get; set; } = true;
		public TimeSpan? UnpauseRemaining { get; set; }
		public TimeSpan GraceTime { get; set; }
		public TimeSpan StartCountdown { get; set; }
		public DateTime? DelayEndsAt { get; set; }
		public bool DelayIsStartCountdown { get; set; }
		public DateTime? NextAdvanceAt { get; set; }
		public string ManageToken { get; set; } = "";

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
				IsPaused = line.IsPaused,
				AutoRestartTimerOnAdvance = line.AutoRestartTimerOnAdvance,
				UnpauseRemaining = line.UnpauseRemaining,
				GraceTime = line.GraceTime,
				StartCountdown = line.StartCountdown,
				DelayEndsAt = line.DelayEndsAt,
				DelayIsStartCountdown = line.DelayIsStartCountdown,
				Description = line.Description,
				LastUpdated = line.LastUpdated,
				NextAdvanceAt = line.NextAdvanceAt,
				ManageToken = line.ManageToken,
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
		public string Id { get; set; } = "";
		public string Name { get; set; } = "";
		public string? Description { get; set; }
		public string Color { get; set; } = "";
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
