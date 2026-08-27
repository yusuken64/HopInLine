using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HopInLine.Data.Line
{
    /// <summary>
    /// Background service that periodically advances lines that are due.
    /// Runs on a 1-second interval, queries for due lines, advances them, and notifies subscribers.
    /// </summary>
    public sealed class LineAdvancementBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LineAdvancementBackgroundService> _logger;

        public LineAdvancementBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<LineAdvancementBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Line advancement service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<ILineRepository>();
                    var notifier = scope.ServiceProvider.GetRequiredService<ILineChangeNotifier>();

                    var dueIds = await repo.GetDueLineIdsAsync(DateTime.UtcNow, stoppingToken);
                    foreach (var id in dueIds)
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        if (await repo.TryAdvanceAsync(id))
                        {
                            await notifier.NotifyAsync(id);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Line advancement sweep failed.");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }

            _logger.LogInformation("Line advancement service stopping.");
        }
    }
}
