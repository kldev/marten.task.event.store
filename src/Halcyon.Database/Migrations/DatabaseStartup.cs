using JasperFx;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Halcyon.Database.Migrations;

/// <summary>
/// Whether a host may bring the schema up to date itself (ADR-0002). One switch for every host:
/// <c>Database:ApplyChangesOnStartup</c>; unset means yes only in Development and Testing.
/// </summary>
public static class DatabaseStartup
{
    public const string ApplyChangesKey = "Database:ApplyChangesOnStartup";
    public const string CommandTimeoutKey = "Database:MigrationCommandTimeoutSeconds";
    public const int DefaultCommandTimeoutSeconds = 900;
    public const string ConnectionStringName = "Postgres";

    private static readonly string[] SelfMigratingEnvironments = ["Development", "Testing"];

    public static bool AppliesChanges(IConfiguration configuration, IHostEnvironment environment) =>
        configuration.GetValue<bool?>(ApplyChangesKey)
        ?? SelfMigratingEnvironments.Contains(environment.EnvironmentName, StringComparer.Ordinal);

    public static AutoCreate SchemaCreation(IConfiguration configuration, IHostEnvironment environment) =>
        AppliesChanges(configuration, environment) ? AutoCreate.CreateOrUpdate : AutoCreate.None;

    /// <summary>The connection string, required: a process that quietly picks localhost changes the wrong database.</summary>
    public static string ConnectionString(IConfiguration configuration)
    {
        var value = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is required (env ConnectionStrings__{ConnectionStringName})."
            );
        return value;
    }

    /// <summary>
    /// The migrator's connection: Npgsql's default 30 s command timeout is too short for an index over a big table, and
    /// Weasel sends a whole patch as one command.
    /// </summary>
    public static string WithMigrationTimeout(string connectionString, IConfiguration configuration)
    {
        var seconds = configuration.GetValue<int?>(CommandTimeoutKey) ?? DefaultCommandTimeoutSeconds;
        if (seconds <= 0)
            throw new InvalidOperationException($"{CommandTimeoutKey} must be a positive number of seconds.");
        return new NpgsqlConnectionStringBuilder(connectionString) { CommandTimeout = seconds }.ConnectionString;
    }
}
