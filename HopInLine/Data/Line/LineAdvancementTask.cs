using Microsoft.Extensions.DependencyInjection;
using System.Numerics;

namespace HopInLine.Data.Line
{
    public class LineAdvancementTask
    {
        public event Action<string> OnStopped;
        private readonly string _lineID;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning;

        public LineAdvancementTask(string lineID, IServiceScopeFactory serviceScopeFactory)
        {
            _lineID = lineID;
            _serviceScopeFactory = serviceScopeFactory;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            Task.Run(async () => await RunAsync(_cancellationTokenSource.Token));
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _cancellationTokenSource.Cancel();
            OnStopped?.Invoke(_lineID);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var lineService = scope.ServiceProvider.GetRequiredService<LineService>();
                    var line = await lineService.GetLineByIdAsync(_lineID);

                    var elapsedTime = DateTime.UtcNow - line.CountDownStart;
                    if (elapsedTime >= line.AutoAdvanceInterval)
                    {
                        line.CountDownStart = DateTime.UtcNow;
                        line.IsPaused = !line.AutoRestartTimerOnAdvance;
                        await lineService.UpdateLineAsync(line);
                        await lineService.AdvanceLineAsync(_lineID);

                    }
                    else
                    {
                        TimeSpan delay = line.AutoAdvanceInterval - elapsedTime;
                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }
        }
    }
}