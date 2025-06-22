using Microsoft.AspNetCore.SignalR;

namespace HopInLine.Data.Line
{
	public class LineUpdatedNotifier
	{
		private readonly IHubContext<LineHub> hubContext;
		private readonly LineAdvancementService advancement;

		public event Func<object, LineChangedEventArgs, Task>? LineUpdated;

		public LineUpdatedNotifier(IHubContext<LineHub> hubContext, LineAdvancementService advancement)
		{
			this.hubContext = hubContext;
			this.advancement = advancement;
			advancement.LineUpdated += async (s, e) =>
			{
				await hubContext.Clients.Group(e.line.Id).SendAsync("UpdateLine", e.line, e.updateId);
				if (LineUpdated != null)
					await LineUpdated.Invoke(this, e);
			};
		}

		internal async Task NotifyLineUpdatedAsync(LineChangedEventArgs lineChangedEventArgs)
		{
			await advancement.OnLineUpdated(lineChangedEventArgs);
		}

		internal void StartLineAdvancement(Line line)
		{
			//throw new NotImplementedException();
		}

		internal void StopLineAdvancement(string id)
		{
			//throw new NotImplementedException();
		}
	}
}