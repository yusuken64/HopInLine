using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HopInLine.Data.Line
{
    /// <summary>
    /// Background service that deletes abandoned lines: those with no user activity (Line.LastUpdated)
    /// for <c>LineRetention:IdleDays</c> days (default 30). Runs at startup and then hourly.
    /// Background auto-advance does not count as activity.
    /// </summary>
    public sealed class LineCleanupBackgroundService : BackgroundService
    {
        public const int DefaultIdleDays = 30;
        private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LineCleanupBackgroundService> _logger;
        private readonly TimeSpan _idleLimit;

        public LineCleanupBackgroundService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<LineCleanupBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            var days = configuration.GetValue("LineRetention:IdleDays", DefaultIdleDays);
            _idleLimit = TimeSpan.FromDays(Math.Max(1, days));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Line cleanup service started; deleting lines idle for {Days} days.", _idleLimit.TotalDays);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<ILineRepository>();

                    var deleted = await repo.DeleteLinesIdleSinceAsync(DateTime.UtcNow - _idleLimit, stoppingToken);
                    if (deleted > 0)
                    {
                        _logger.LogInformation("Deleted {Count} idle lines.", deleted);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Line cleanup sweep failed.");
                }

                try { await Task.Delay(SweepInterval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }

            _logger.LogInformation("Line cleanup service stopping.");
        }
    }
}
