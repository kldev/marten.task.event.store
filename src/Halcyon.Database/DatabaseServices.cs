using Halcyon.Database.Migrations;
using Halcyon.Database.Seed;
using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Marten;

namespace Halcyon.Database;

public static class DatabaseServices
{
    /// <summary>
    /// What a process that only migrates needs (the migrator, the tests): the store and Wolverine for its message store
    /// alone - no handler, no listener, and the host is never started (ADR-0002, ADR-0016).
    /// </summary>
    public static IHostApplicationBuilder AddHalcyonMigration(
        this IHostApplicationBuilder builder,
        string connectionString
    )
    {
        builder.Services.AddHalcyonDatabase(connectionString, AutoCreate.None);
        builder.UseWolverine(options =>
        {
            options.AutoBuildMessageStorageOnStartup = AutoCreate.None;
            options.Discovery.DisableConventionalDiscovery();
        });
        return builder;
    }

    /// <summary>
    /// One <c>NpgsqlDataSource</c> for the process and Marten on top of it (ADR-0002), the schema migrator and the seed.
    /// With <paramref name="wolverine"/> Marten is integrated with Wolverine (ADR-0016): its sessions enlist in the
    /// transactional outbox, and the inbox/outbox tables live in <see cref="HalcyonStore.MessageSchema"/> on the same
    /// pool. The caller must then also call <c>UseWolverine</c> (the app, the migrator); a process that only reads or
    /// arranges data (the e2e helpers) leaves it off.
    /// </summary>
    public static IServiceCollection AddHalcyonDatabase(
        this IServiceCollection services,
        string connectionString,
        AutoCreate schemaCreation,
        bool wolverine = true
    )
    {
        services.AddNpgsqlDataSource(connectionString);
        var marten = services
            .AddMarten(options =>
            {
                HalcyonStore.Configure(options);
                options.AutoCreateSchemaObjects = schemaCreation;
            })
            .UseNpgsqlDataSource()
            .UseLightweightSessions();
        if (wolverine)
            marten.IntegrateWithWolverine(x => x.MessageStorageSchemaName = HalcyonStore.MessageSchema);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SchemaMigrator>();
        services.AddSingleton<HalcyonSeeder>();
        return services;
    }
}
