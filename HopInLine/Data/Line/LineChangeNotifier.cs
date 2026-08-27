using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace HopInLine.Data.Line
{
	/// <summary>
	/// In-process singleton pub/sub implementation for line change notifications.
	/// Thread-safe and holds no DbContext state.
	///
	/// Handlers are invoked on whatever thread calls NotifyAsync (typically a background scheduler thread),
	/// so Blazor components must marshal via InvokeAsync.
	/// </summary>
	public class LineChangeNotifier : ILineChangeNotifier
	{
		private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
		private readonly ILogger<LineChangeNotifier> _logger;

		private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Func<LineDto, Task>>> _subscribers = new();

		public LineChangeNotifier(
			IDbContextFactory<ApplicationDbContext> contextFactory,
			ILogger<LineChangeNotifier> logger)
		{
			_contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// Subscribes a handler to notifications for a specific line.
		/// Returns an IDisposable that removes the handler when disposed.
		/// </summary>
		public IDisposable Subscribe(string lineId, Func<LineDto, Task> handler)
		{
			if (string.IsNullOrWhiteSpace(lineId))
			{
				throw new ArgumentException("lineId cannot be null or empty", nameof(lineId));
			}

			if (handler == null)
			{
				throw new ArgumentNullException(nameof(handler));
			}

			var token = Guid.NewGuid();

			var innerDict = _subscribers.GetOrAdd(lineId, _ => new ConcurrentDictionary<Guid, Func<LineDto, Task>>());
			innerDict.TryAdd(token, handler);

			return new Subscription(this, lineId, token);
		}

		/// <summary>
		/// Loads the current line and notifies all subscribers for that line ID.
		/// If no subscribers exist for the line, returns immediately without querying the database.
		/// Handlers are invoked on the calling thread (typically a background scheduler thread).
		/// </summary>
		public async Task NotifyAsync(string lineId)
		{
			// Early exit: if no one is subscribed, don't touch the database
			if (!_subscribers.TryGetValue(lineId, out var subscribers) || subscribers.IsEmpty)
			{
				return;
			}

			try
			{
				// Load the line with participants included
				Line? line;
				try
				{
					using (var context = _contextFactory.CreateDbContext())
					{
						line = await context.Lines
							.AsNoTracking()
							.Include(l => l.Participants)
							.FirstOrDefaultAsync(l => l.Id == lineId);
					}
				}
				catch (OperationCanceledException ex)
				{
					_logger.LogDebug(ex, "Line load operation was cancelled for lineId: {LineId}", lineId);
					return;
				}

				// If line no longer exists, log and return
				if (line == null)
				{
					_logger.LogDebug("Line {LineId} no longer exists when attempting to notify subscribers", lineId);
					return;
				}

				// Project to LineDto
				var lineDto = LineDto.FromLine(line);

				// Snapshot the handlers before iterating to prevent issues if a handler disposes the subscription
				var handlers = subscribers.Values.ToArray();

				// Invoke each handler, catching exceptions so one failure doesn't prevent others
				foreach (var handlerFunc in handlers)
				{
					try
					{
						await handlerFunc(lineDto);
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Handler failed for line {LineId}", lineId);
					}
				}
			}
			catch (OperationCanceledException ex)
			{
				_logger.LogDebug(ex, "NotifyAsync operation was cancelled for lineId: {LineId}", lineId);
			}
		}

		/// <summary>
		/// Private nested class implementing IDisposable for subscription cleanup.
		/// Removes the handler from the subscription collection when disposed.
		/// Dispose is idempotent and thread-safe.
		/// </summary>
		private class Subscription : IDisposable
		{
			private readonly LineChangeNotifier _notifier;
			private readonly string _lineId;
			private readonly Guid _token;
			private bool _disposed;

			public Subscription(LineChangeNotifier notifier, string lineId, Guid token)
			{
				_notifier = notifier;
				_lineId = lineId;
				_token = token;
				_disposed = false;
			}

			public void Dispose()
			{
				// Idempotent: calling multiple times is safe
				if (_disposed)
				{
					return;
				}

				_disposed = true;

				// Remove the handler by token
				if (_notifier._subscribers.TryGetValue(_lineId, out var innerDict))
				{
					innerDict.TryRemove(_token, out _);

					// If the inner dictionary is now empty, remove the line's key
					if (innerDict.IsEmpty)
					{
						_notifier._subscribers.TryRemove(_lineId, out _);
					}
				}
			}
		}
	}
}
