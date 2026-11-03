using Halcyon.Database.Migrations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Halcyon.App.Infrastructure;

/// <summary>Readiness: the database answers and the schema matches this version (ADR-0002).</summary>
public sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource, SchemaMigrator migrator) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await using var command = dataSource.CreateCommand("select 1");
            await command.ExecuteScalarAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database unreachable", exception);
        }

        return await migrator.PendingAsync(cancellationToken) is null
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Schema behind this version - run Halcyon.DbMigrator");
    }
}
