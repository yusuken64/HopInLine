using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace HopInLine.Data.Line
{
    public class LineAdvancementService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ConcurrentDictionary<string, LineAdvancementTask> _tasks = new();

        public delegate Task AsyncEventHandler<TEventArgs>(object sender, TEventArgs e);
        public event AsyncEventHandler<LineChangedEventArgs> LineUpdated;

        public async Task OnLineUpdated(LineChangedEventArgs e)
        {
            if (LineUpdated != null)
            {
                await LineUpdated.Invoke(this, e);
            }
        }

        public LineAdvancementService(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
            LineUpdated += async (sender, args) =>
            {
                StartLineAdvancement(args.line.Id);
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await StartAllLinesAsync(stoppingToken);
            // Keep the service alive indefinitely
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            StopAllLines();
            await base.StopAsync(stoppingToken);
        }

        public void StartLineAdvancement(string lineId)
        {
            if (_tasks.TryGetValue(lineId, out var existingTask))
            {
                existingTask.Stop();
                _tasks.Remove(lineId, out _);
            }

            var task = new LineAdvancementTask(lineId, _serviceScopeFactory);
            _tasks[lineId] = task;
            task.OnStopped += (id) => _tasks.Remove(id, out _);
            task.Start();
		}

		internal void ResumeLineAdvancement(string lineId)
		{
			if (_tasks.TryGetValue(lineId, out var existingTask))
			{
				existingTask.Stop();
				_tasks.Remove(lineId, out _);
			}

			var task = new LineAdvancementTask(lineId, _serviceScopeFactory);
			_tasks[lineId] = task;
			task.OnStopped += (id) => _tasks.Remove(id, out _);
			task.Start();
		}

		public void StopLineAdvancement(string lineId)
        {
            if (_tasks.TryGetValue(lineId, out var task))
            {
                task.Stop();
                _tasks.Remove(lineId, out var _);
            }
        }

        public async Task StartAllLinesAsync(CancellationToken cancellationToken)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var lineRepository = scope.ServiceProvider.GetRequiredService<ILineRepository>();
                var lines = await lineRepository.GetAllLinesAsync(cancellationToken);
                foreach (var line in lines.Where(x => x.AutoAdvanceLine))
                {
                    try
                    {
                        StartLineAdvancement(line.Id);
                    }
                    catch (Exception ex)
                    {
                        // Log or handle specific line start failures
                    }
                }
            }
        }

        public void StopAllLines()
        {
            foreach (var key in _tasks.Keys)
            {
                StopLineAdvancement(key);
            }
        }
	}
}