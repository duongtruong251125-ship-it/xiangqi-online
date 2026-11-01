using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using XiangqiOnline.Infrastructure.Persistence;

namespace XiangqiOnline.API.Infrastructure;

public sealed class SqlServerHealthCheck(AppDbContext db, ILogger<SqlServerHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy("Database reachable.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "SQL Server health check failed.");
            return HealthCheckResult.Unhealthy("Database unavailable.");
        }
    }
}
