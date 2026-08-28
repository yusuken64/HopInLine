using Microsoft.Extensions.Logging;

namespace HopInLine.Data.Line
{
	public class DebugLineOwnerNotifier : ILineOwnerNotifier
	{
		private readonly ILogger<DebugLineOwnerNotifier> _logger;

		public DebugLineOwnerNotifier(ILogger<DebugLineOwnerNotifier> logger)
		{
			_logger = logger;
		}

		public Task NotifyLineCreatedAsync(string ownerEmail, string lineName, string viewUrl, string manageUrl, CancellationToken cancellationToken = default)
		{
			_logger.LogInformation(
				"Debug owner notifier: suppressed line-created email. To: {OwnerEmail}, Line: {LineName}, View: {ViewUrl}, Manage: {ManageUrl}",
				ownerEmail, lineName, viewUrl, manageUrl);

			return Task.CompletedTask;
		}
	}
}
