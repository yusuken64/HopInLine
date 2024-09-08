using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace HopInLine.Data.Line
{
    public class LineHub : Hub
    {
		public static uint updateId;
        private readonly IServiceScopeFactory serviceScopeFactory;
		private readonly LineAdvancementService lineAdvancementService;

		public LineHub(IServiceScopeFactory serviceScopeFactory, LineAdvancementService lineAdvancementService)
        {
            this.serviceScopeFactory = serviceScopeFactory;
			this.lineAdvancementService = lineAdvancementService;

			lineAdvancementService.LineUpdated += LineAdvancementService_LineUpdated;
		}

		private async Task LineAdvancementService_LineUpdated(object sender, LineChangedEventArgs e)
		{
			await Clients.Group(e.line.Id).SendAsync("UpdateLine", e.line, updateId++);
		}

		public async Task JoinLineGroup(string lineID)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, lineID);
        }

        public async Task LeaveLineGroup(string lineID)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, lineID);
        }

        public async Task AddParticipant(string lineID, Participant participant)
        {
			using var scope = serviceScopeFactory.CreateAsyncScope();
			var lineService = scope.ServiceProvider.GetRequiredService<LineService>();
			await lineService.AddParticipantAsync(lineID, participant);
		}

        public async Task AdvanceLine(string lineID)
        {
			using var scope = serviceScopeFactory.CreateAsyncScope();
			var lineService = scope.ServiceProvider.GetRequiredService<LineService>();
			await lineService.AdvanceLineAsync(lineID);
		}

        public async Task MoveParticipantUp(string lineID, string instanceId)
        {
			using var scope = serviceScopeFactory.CreateAsyncScope();
			var lineService = scope.ServiceProvider.GetRequiredService<LineService>();
			await lineService.MoveParticipantUpAsync(lineID, instanceId);
		}

        public async Task MoveParticipantDown(string lineID, string instanceId)
        {
			using var scope = serviceScopeFactory.CreateAsyncScope();
			var lineService = scope.ServiceProvider.GetRequiredService<LineService>();
			await lineService.MoveParticipantDownAsync(lineID, instanceId);
		}

        public async Task RemoveParticipant(string lineID, string instanceId)
        {
			using var scope = serviceScopeFactory.CreateAsyncScope();
			var lineService = scope.ServiceProvider.GetRequiredService<LineService>();
			await lineService.RemoveParticipantAsync(lineID, instanceId);
		}

        public async Task ReAddRemovedParticipant(string lineID, string instanceId)
        {
			using var scope = serviceScopeFactory.CreateAsyncScope();
			var lineService = scope.ServiceProvider.GetRequiredService<LineService>();
			await lineService.ReAddRemovedParticipantAsync(lineID, instanceId);
		}

        public async Task DeleteRemovedParticipant(string lineID, string instanceId)
        {
			using var scope = serviceScopeFactory.CreateAsyncScope();
			var lineService = scope.ServiceProvider.GetRequiredService<LineService>();
			await lineService.DeleteRemovedParticipantAsync(lineID, instanceId);
		}

		public async Task StartTimer(string lineId)
		{
			using var scope = serviceScopeFactory.CreateAsyncScope();
			var lineService = scope.ServiceProvider.GetRequiredService<LineService>();
			await lineService.StartTimerAsync(lineId);
		}

		public async Task StopTimer(string lineId)
		{
			using var scope = serviceScopeFactory.CreateAsyncScope();
			var lineService = scope.ServiceProvider.GetRequiredService<LineService>();
			await lineService.StopTimerAsync(lineId);
		}

		public override async Task OnDisconnectedAsync(Exception exception)
		{
			lineAdvancementService.LineUpdated -= LineAdvancementService_LineUpdated;
			// Handle any cleanup or removal from groups
			await base.OnDisconnectedAsync(exception);
        }
    }

}
