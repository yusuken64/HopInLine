using Microsoft.Extensions.DependencyInjection;
using System.Numerics;

namespace HopInLine.Data.Line
{
    public class LineAdvancementTask
    {
        private readonly Line _line;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private CancellationTokenSource _cancellationTokenSource;

        public LineAdvancementTask(Line line, IServiceScopeFactory serviceScopeFactory)
        {
            _line = line;
            _serviceScopeFactory = serviceScopeFactory;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            Task.Run(async () => await RunAsync(_cancellationTokenSource.Token));
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var elapsedTime = DateTime.UtcNow - _line.CountDownStart;
                if (elapsedTime >= _line.AutoAdvanceInterval)
                {
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        Stop();
                        var lineService = scope.ServiceProvider.GetRequiredService<LineService>();
                        await lineService.AdvanceLineAsync(_line.Id);
                        _line.CountDownStart = DateTime.UtcNow;
                    }
                }
                else
                {
                    TimeSpan delay = _line.AutoAdvanceInterval - elapsedTime;
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
    }
}