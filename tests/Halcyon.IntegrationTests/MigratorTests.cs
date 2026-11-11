using Halcyon.Database;
using Halcyon.Database.Migrations;
using Halcyon.IntegrationTests.Infrastructure;
using JasperFx;
using Marten;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Weasel.Postgresql;

namespace Halcyon.IntegrationTests;

/// <summary>Spec 007 / ADR-0002: the schema changes only through the migrator.</summary>
public class MigratorTests(PostgresContainer postgres)
{
    private static async Task<string?> PendingAsync(string connectionString)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddHalcyonDatabase(connectionString, AutoCreate.None)
            .BuildServiceProvider();
        await using var _ = services;
        return await services.GetRequiredService<SchemaMigrator>().PendingAsync();
    }

    [Fact]
    public async Task Migrator_OnAnEmptyDatabase_CreatesTheSchema_AndASecondRunFindsNothing()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        (await PendingAsync(connectionString)).ShouldNotBeNull();

        (await Migrator.RunAsync(connectionString)).ShouldBe(0);

        (await PendingAsync(connectionString)).ShouldBeNull();
        (await Migrator.RunAsync(connectionString)).ShouldBe(0);
        (await Migrator.RunAsync(connectionString, "--dry-run")).ShouldBe(0);
        (await PendingAsync(connectionString)).ShouldBeNull();
    }

    [Fact]
    public async Task DryRun_ChangesNothing()
    {
        var connectionString = await postgres.CreateDatabaseAsync();

        (await Migrator.RunAsync(connectionString, "--dry-run")).ShouldBe(0);

        (await PendingAsync(connectionString)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Migrator_WithoutConnectionString_Fails() => (await DbMigrator.Program.Main([])).ShouldBe(1);

    [Fact]
    public async Task Migrator_OnAnUnreachableDatabase_Fails() =>
        (await Migrator.RunAsync("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2")).ShouldBe(1);

    [Fact]
    public async Task Host_OnADatabaseThatIsBehind_RefusesToStart_AndNamesTheMigrator()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var app = TestApp.Build(connectionString, null, null, out _);

        var refusal = await Should.ThrowAsync<InvalidOperationException>(() => app.Services.PrepareDatabaseAsync());

        refusal.Message.ShouldContain("DbMigrator");
        (await PendingAsync(connectionString)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Host_OnAMigratedDatabase_Starts()
    {
        await using var app = await TestApp.StartAsync(postgres);
        await Should.NotThrowAsync(() => app.Services.PrepareDatabaseAsync());
    }

    [Fact]
    public async Task Host_WithApplyChangesOn_MigratesItself()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var app = TestApp.Build(
            connectionString,
            new() { ["Database:ApplyChangesOnStartup"] = "true" },
            null,
            out _
        );

        await app.Services.PrepareDatabaseAsync();

        (await PendingAsync(connectionString)).ShouldBeNull();
    }

    [Fact]
    public async Task Marten_UsesTheOneRegisteredDataSource()
    {
        await using var app = await TestApp.StartAsync(postgres);
        var registered = app.Get<NpgsqlDataSource>();
        var database = app.Store.Storage.Database.ShouldBeAssignableTo<PostgresqlDatabase>();

        database.DataSource.ShouldBeSameAs(registered);
    }

    [Fact]
    public async Task Health_IsAnonymous_AndReadinessChecksTheDatabase()
    {
        await using var app = await TestApp.StartAsync(postgres);
        using var client = app.Client();

        (await client.GetAsync(new Uri("/health", UriKind.Relative))).StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await client.GetAsync(new Uri("/health/ready", UriKind.Relative))).StatusCode.ShouldBe(
            System.Net.HttpStatusCode.OK
        );
    }

    [Fact]
    public async Task Readiness_OnADatabaseThatIsBehind_IsUnhealthy()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var app = TestApp.Build(connectionString, null, null, out _);
        await app.StartAsync();
        using var client = app.GetTestClient();

        (await client.GetAsync(new Uri("/health/ready", UriKind.Relative))).StatusCode.ShouldBe(
            System.Net.HttpStatusCode.ServiceUnavailable
        );
        await app.StopAsync();
    }

    [Fact]
    public async Task SchemaMigrator_ApplyTwice_ReportsNoChangeTheSecondTime()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var services = new ServiceCollection()
            .AddLogging()
            .AddHalcyonDatabase(connectionString, AutoCreate.None)
            .BuildServiceProvider();
        await using var _ = services;
        var migrator = new SchemaMigrator(
            services.GetRequiredService<IDocumentStore>(),
            NullLogger<SchemaMigrator>.Instance
        );

        (await migrator.ApplyAsync(dryRun: false)).ShouldBeTrue();
        (await migrator.ApplyAsync(dryRun: false)).ShouldBeFalse();
    }
}
