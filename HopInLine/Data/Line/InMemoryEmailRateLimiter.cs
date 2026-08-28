using System.Collections.Concurrent;

namespace HopInLine.Data.Line
{
	public class InMemoryEmailRateLimiter : IEmailRateLimiter
	{
		private const int MaxPerWindow = 5;
		private static readonly TimeSpan Window = TimeSpan.FromHours(1);

		private readonly ConcurrentDictionary<string, Queue<DateTime>> _sendTimestamps = new();

		public bool TryConsume(string email)
		{
			var key = email.Trim().ToLowerInvariant();
			var queue = _sendTimestamps.GetOrAdd(key, _ => new Queue<DateTime>());

			lock (queue)
			{
				var now = DateTime.UtcNow;
				while (queue.Count > 0 && now - queue.Peek() > Window)
				{
					queue.Dequeue();
				}

				if (queue.Count >= MaxPerWindow)
				{
					return false;
				}

				queue.Enqueue(now);
				return true;
			}
		}
	}
}
