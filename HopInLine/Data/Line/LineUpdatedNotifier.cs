namespace HopInLine.Data.Line
{
    public class LineUpdatedNotifier
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public LineUpdatedNotifier(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task NotifyLineUpdatedAsync(LineChangedEventArgs args)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateAsyncScope())
                {
                    var lineRepository = scope.ServiceProvider.GetRequiredService<ILineRepository>();
                    await lineRepository.UpdateLineAsync(args.line);

                    var updatedLine = await lineRepository.GetLineAsync(args.line.Id);
                    var lineAdvancementService = scope.ServiceProvider.GetRequiredService<LineAdvancementService>();
                    await lineAdvancementService.OnLineUpdated(new LineChangedEventArgs(updatedLine));
                }
            } catch (Exception ex)
            {

            }
        }

        internal void StartLineAdvancement(Line line)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var lineAdvancementService = scope.ServiceProvider.GetRequiredService<LineAdvancementService>();
                lineAdvancementService.StartLineAdvancement(line);
            }
        }

        internal void StopLineAdvancement(string id)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var lineAdvancementService = scope.ServiceProvider.GetRequiredService<LineAdvancementService>();
                lineAdvancementService.StopLineAdvancement(id);
            }
        }
    }
}