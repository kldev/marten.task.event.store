using Halcyon.IntegrationTests.Infrastructure;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresContainer))]

namespace Halcyon.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL 18 container for the whole run (ADR-0007); every test class gets its own empty database, so classes
/// never see each other's data.
/// </summary>
public sealed class PostgresContainer : IAsyncLifetime
{
    // Test classes run in parallel, each with its own host, and every host keeps a few connections for Wolverine's
    // durability agent (ADR-0016): more than PostgreSQL's default 100 at once.
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18")
        .WithCommand("-c", "max_connections=500")
        .Build();

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"t_{Guid.NewGuid():N}"[..20];
        await using (var connection = new NpgsqlConnection(container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"create database {name}", connection);
            await command.ExecuteNonQueryAsync();
        }
        return new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = name }.ConnectionString;
    }
}
