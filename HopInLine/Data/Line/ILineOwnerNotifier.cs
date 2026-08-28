namespace HopInLine.Data.Line
{
	public interface ILineOwnerNotifier
	{
		Task NotifyLineCreatedAsync(string ownerEmail, string lineName, string viewUrl, string manageUrl, CancellationToken cancellationToken = default);
	}
}
