namespace HopInLine.Data.Line
{
	public interface IEmailRateLimiter
	{
		bool TryConsume(string email);
	}
}
