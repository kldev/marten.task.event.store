using Halcyon.Database;
using Halcyon.Database.Migrations;
using Halcyon.Database.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Halcyon.DbMigrator;

/// <summary>
/// <c>Halcyon.DbMigrator [apply] [--dry-run]</c> brings the schema in line with this version and exits;
/// <c>--dry-run</c> prints the pending SQL and changes nothing. <c>Halcyon.DbMigrator seed</c> applies the schema and
/// then inserts the missing demo data - in Polish, or in English with <c>Seed:Language=en</c> (<c>Seed__Language</c>,
/// ADR-0012; for an empty database: records already there are never rewritten). Exit code 0 on success, 1 on failure (ADR-0002, ADR-0006).
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var dryRun = args.Contains("--dry-run");
        var seed = args.Contains("seed");
        var hostArgs = args.Where(a => a is not ("--dry-run" or "seed" or "apply")).ToArray();

        var builder = Host.CreateApplicationBuilder(hostArgs);

        string connectionString;
        try
        {
            connectionString = DatabaseStartup.WithMigrationTimeout(
                DatabaseStartup.ConnectionString(builder.Configuration),
                builder.Configuration
            );
        }
        catch (InvalidOperationException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return 1;
        }

        // The store changes nothing by itself: every change goes through SchemaMigrator, so a dry run really is dry.
        builder.AddHalcyonMigration(connectionString);

        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Halcyon.DbMigrator");

        try
        {
            await host.Services.GetRequiredService<SchemaMigrator>().ApplyAsync(dryRun);
            if (seed && !dryRun)
                await host
                    .Services.GetRequiredService<HalcyonSeeder>()
                    .RunAsync(builder.Configuration["Seed:Language"] == "en" ? SeedEnglish.Definition : null);
            return 0;
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Database migration failed");
            return 1;
        }
    }
}
