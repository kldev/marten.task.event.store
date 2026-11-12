using Halcyon.PersistenceTests.Infrastructure;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresContainer))]

namespace Halcyon.PersistenceTests.Infrastructure;

/// <summary>
/// One PostgreSQL 18 container for the run; every test class gets its own empty database (ADR-0007). A copy of the
/// integration tests' container rather than a shared library: the two projects reference nothing in common.
/// </summary>
public sealed class PostgresContainer : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"p_{Guid.NewGuid():N}"[..20];
        await using (var connection = new NpgsqlConnection(container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"create database {name}", connection);
            await command.ExecuteNonQueryAsync();
        }
        return new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = name }.ConnectionString;
    }
}
