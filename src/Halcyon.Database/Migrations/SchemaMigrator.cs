using JasperFx;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Weasel.Core;
using Weasel.Core.Migrations;

namespace Halcyon.Database.Migrations;

/// <summary>
/// Brings the database in line with <see cref="HalcyonStore"/> (documents, event store, projections) in one patch, and
/// tells what is pending. Applying twice is a no-op.
/// </summary>
public sealed class SchemaMigrator(IDocumentStore store, ILogger<SchemaMigrator> logger)
{
    private IDatabase Database => store.Storage.Database;

    /// <summary>The SQL that applying would run, or null when the database is up to date.</summary>
    public async Task<string?> PendingAsync(CancellationToken ct = default)
    {
        var migration = await Database.CreateMigrationAsync(ct);
        if (migration.Difference == SchemaPatchDifference.None)
            return null;

        await using var writer = new StringWriter();
        migration.WriteAllUpdates(writer, Database.Migrator, AutoCreate.CreateOrUpdate);
        return writer.ToString();
    }

    /// <summary>Applies the pending changes and checks that nothing is left. Returns whether anything changed.</summary>
    public async Task<bool> ApplyAsync(bool dryRun, CancellationToken ct = default)
    {
        var pending = await PendingAsync(ct);
        if (pending is null)
        {
            logger.LogInformation("Schema: up to date");
            return false;
        }

        if (dryRun)
        {
            logger.LogInformation("Schema: pending, not applied (dry run){NewLine}{Sql}", Environment.NewLine, pending);
            return true;
        }

        logger.LogInformation("Schema: applying");
        await Database.ApplyAllConfiguredChangesToDatabaseAsync(AutoCreate.CreateOrUpdate, ct: ct);

        // Trust, but check: a patch that reports success and leaves work behind would fail the first request instead.
        if (await PendingAsync(ct) is { } left)
            throw new InvalidOperationException(
                $"Schema still has pending changes after applying:{Environment.NewLine}{left}"
            );

        logger.LogInformation("Schema: applied");
        return true;
    }
}

public static class SchemaStartupExtensions
{
    /// <summary>
    /// Called by the app before it serves anything (ADR-0002). Where it may migrate, it applies; where it may not, it
    /// refuses to start on a database that is behind - a crash naming the migrator beats failing every request.
    /// </summary>
    public static async Task PrepareDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var migrator = services.GetRequiredService<SchemaMigrator>();

        // Both branches build the patch of every configured type, so a bad document mapping fails the start here,
        // not the first request that touches the type.
        if (DatabaseStartup.AppliesChanges(configuration, environment))
        {
            await migrator.ApplyAsync(dryRun: false, ct);
            return;
        }

        if (await migrator.PendingAsync(ct) is not null)
            throw new InvalidOperationException(
                $"The database schema is behind this version and {DatabaseStartup.ApplyChangesKey} is off - "
                    + "run Halcyon.DbMigrator first (docker compose runs it as the 'migrator' service)."
            );
    }
}
