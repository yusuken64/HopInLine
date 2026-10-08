using System.Threading.RateLimiting;

namespace HopInLine.Data.Line
{
	/// <summary>
	/// What a rate-limited call does. Each kind has its own budget.
	/// </summary>
	public enum RateLimitedAction
	{
		/// <summary>Creating a line.</summary>
		Create,
		/// <summary>Any other change: join, leave, advance, edit, delete, timers...</summary>
		Write,
		/// <summary>Loading a line or validating a manage token (guards against token guessing).</summary>
		Read,
	}

	/// <summary>
	/// Identifies who is calling, for rate limiting. Scoped: one per Blazor circuit (browser tab).
	/// <see cref="ClientIp"/> is captured from the HTTP request in _Host.cshtml and set by App.razor.
	/// </summary>
	public sealed class ClientContext
	{
		public string ConnectionId { get; } = Guid.NewGuid().ToString("N");
		public string ClientIp { get; set; } = "unknown";
	}

	public interface IActionRateLimiter
	{
		/// <summary>Consumes one permit for this client; false if the client is over its limit.</summary>
		bool TryConsume(RateLimitedAction action, ClientContext client);

		/// <summary>Consumes one permit from the per-line join budget; false if the line is being flooded.</summary>
		bool TryConsumeJoin(string lineId);
	}

	/// <summary>
	/// In-process sliding-window limits. Each client call is checked against two budgets:
	/// a tight one per connection (tab) and a looser one per IP, since a whole office may share one IP.
	/// Partitions for idle keys are evicted automatically. Each server instance enforces its own windows.
	/// </summary>
	public sealed class InMemoryActionRateLimiter : IActionRateLimiter, IDisposable
	{
		public const int MaxJoinsPerLinePerMinute = 20;

		// (permits, window) per action, for one connection and for one IP
		private static readonly Dictionary<RateLimitedAction, (int Permits, TimeSpan Window)> PerConnection = new()
		{
			[RateLimitedAction.Create] = (5, TimeSpan.FromMinutes(10)),
			[RateLimitedAction.Write] = (60, TimeSpan.FromMinutes(1)),
			[RateLimitedAction.Read] = (120, TimeSpan.FromMinutes(1)),
		};

		private static readonly Dictionary<RateLimitedAction, (int Permits, TimeSpan Window)> PerIp = new()
		{
			[RateLimitedAction.Create] = (30, TimeSpan.FromHours(1)),
			[RateLimitedAction.Write] = (600, TimeSpan.FromMinutes(1)),
			[RateLimitedAction.Read] = (1200, TimeSpan.FromMinutes(1)),
		};

		public static int ConnectionLimit(RateLimitedAction action) => PerConnection[action].Permits;

		private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(key =>
		{
			var (permits, window) = LimitFor(key);
			return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
			{
				PermitLimit = permits,
				Window = window,
				SegmentsPerWindow = 6,
				QueueLimit = 0,
				AutoReplenishment = true
			});
		});

		public bool TryConsume(RateLimitedAction action, ClientContext client)
		{
			return Acquire($"conn|{action}|{client.ConnectionId}")
				&& Acquire($"ip|{action}|{client.ClientIp}");
		}

		public bool TryConsumeJoin(string lineId) => Acquire($"line|Join|{lineId}");

		private bool Acquire(string key)
		{
			using var lease = _limiter.AttemptAcquire(key);
			return lease.IsAcquired;
		}

		// Keys are "scope|action|id"
		private static (int, TimeSpan) LimitFor(string key)
		{
			var parts = key.Split('|', 3);
			if (parts[0] == "line")
			{
				return (MaxJoinsPerLinePerMinute, TimeSpan.FromMinutes(1));
			}
			var action = Enum.Parse<RateLimitedAction>(parts[1]);
			return parts[0] == "ip" ? PerIp[action] : PerConnection[action];
		}

		public void Dispose() => _limiter.Dispose();
	}

	/// <summary>
	/// Thrown when a client is over a rate limit. Derives from InvalidOperationException so existing
	/// UI error handling shows the message.
	/// </summary>
	public sealed class RateLimitExceededException : InvalidOperationException
	{
		public RateLimitExceededException(string message) : base(message) { }
	}
}
