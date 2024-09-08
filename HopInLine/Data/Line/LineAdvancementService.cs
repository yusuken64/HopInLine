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
        public void StartLineAdvancement(Line line)
        {
            if (!_tasks.ContainsKey(line.Id))
            {
                var task = new LineAdvancementTask(line, _serviceScopeFactory);
                _tasks[line.Id] = task;
                task.Start();
            }
            else
            {
                throw new Exception("line already started");
            }
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
                foreach (var line in lines)
                {
                    StartLineAdvancement(line);
                }
            }
        }

        public void StopAllLines()
        {
            foreach (var task in _tasks.Values)
            {
                task.Stop();
            }
            _tasks.Clear();
        }
    }
}