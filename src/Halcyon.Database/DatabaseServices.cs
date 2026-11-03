using Halcyon.Database.Migrations;
using Halcyon.Database.Seed;
using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Halcyon.Database;

public static class DatabaseServices
{
    /// <summary>
    /// One <c>NpgsqlDataSource</c> for the process and Marten on top of it (ADR-0002), the schema migrator and the seed.
    /// </summary>
    public static IServiceCollection AddHalcyonDatabase(
        this IServiceCollection services,
        string connectionString,
        AutoCreate schemaCreation
    )
    {
        services.AddNpgsqlDataSource(connectionString);
        services
            .AddMarten(options =>
            {
                HalcyonStore.Configure(options);
                options.AutoCreateSchemaObjects = schemaCreation;
            })
            .UseNpgsqlDataSource()
            .UseLightweightSessions();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SchemaMigrator>();
        services.AddSingleton<HalcyonSeeder>();
        return services;
    }
}
