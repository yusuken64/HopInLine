using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HopInLine.Data
{
	/// <summary>
	/// Reports unhealthy when the database can't be reached, so the orchestrator stops routing to this instance.
	/// </summary>
	public class DatabaseHealthCheck : IHealthCheck
	{
		private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

		public DatabaseHealthCheck(IDbContextFactory<ApplicationDbContext> contextFactory)
		{
			_contextFactory = contextFactory;
		}

		public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
		{
			await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
			return await db.Database.CanConnectAsync(cancellationToken)
				? HealthCheckResult.Healthy()
				: HealthCheckResult.Unhealthy("Cannot connect to the database.");
		}
	}
}
