using Halcyon.App;
using Halcyon.App.Storage;
using Halcyon.Database.Documents;
using Halcyon.Database.Seed;
using Halcyon.Domain.People;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Halcyon.IntegrationTests.Infrastructure;

/// <summary>
/// The real host (<see cref="HalcyonApp.Build"/>) on a test server, in <c>Testing</c>, on a database prepared by the
/// production migrator. Schema changes by the host are off, as in a release (ADR-0002).
/// </summary>
public sealed class TestApp : IAsyncDisposable
{
    public const string Password = "test-password";

    private TestApp(WebApplication app, FakeTimeProvider clock)
    {
        App = app;
        Clock = clock;
    }

    private WebApplication App { get; }
    public FakeTimeProvider Clock { get; }
    public IServiceProvider Services => App.Services;
    public IDocumentStore Store => Services.GetRequiredService<IDocumentStore>();

    public T Get<T>()
        where T : notnull => Services.GetRequiredService<T>();

    public HttpClient Client() => App.GetTestClient();

    /// <summary>Migrates (production migrator), builds and starts the host.</summary>
    public static async Task<TestApp> StartAsync(
        PostgresContainer postgres,
        bool migrate = true,
        Dictionary<string, string?>? settings = null,
        DateTimeOffset? now = null
    )
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        if (migrate)
            (await Migrator.RunAsync(connectionString)).ShouldBe(0);

        var app = Build(connectionString, settings, now, out var clock);
        await app.StartAsync();
        return new TestApp(app, clock);
    }

    public static WebApplication Build(
        string connectionString,
        Dictionary<string, string?>? settings,
        DateTimeOffset? now,
        out FakeTimeProvider clock
    )
    {
        var fake = new FakeTimeProvider(now ?? DateTimeOffset.UtcNow);
        clock = fake;
        return HalcyonApp.Build(
            ["--environment", "Testing"],
            builder =>
            {
                builder.WebHost.UseTestServer();
                var values = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = connectionString,
                    ["Auth:AgentPassword"] = Password,
                    ["Database:ApplyChangesOnStartup"] = "false",
                    ["Storage:ServiceUrl"] = "http://storage.invalid",
                    ["Storage:AccessKey"] = "test",
                    ["Storage:SecretKey"] = "test",
                };
                // Files in memory, unless the test brings a real S3 (RustFsStorageTests).
                if (settings?.ContainsKey("Storage:ServiceUrl") != true)
                    builder.Services.AddSingleton<IAttachmentStorage, InMemoryAttachmentStorage>();
                foreach (var (key, value) in settings ?? [])
                    values[key] = value;
                builder.Configuration.AddInMemoryCollection(values);
                builder.Services.AddSingleton<TimeProvider>(fake);
            }
        );
    }

    /// <summary>Adds agents and people (the seed's ones) without tickets.</summary>
    public async Task SeedPeopleAsync() =>
        await Get<HalcyonSeeder>().RunAsync(new SeedDefinition(SeedData.Agents, SeedData.People, []));

    public async Task<AgentRef> AgentAsync(string email)
    {
        await using var session = Store.QuerySession();
        var normalized = Emails.Normalize(email);
        var agent = await session.Query<Agent>().SingleAsync(a => a.EmailNormalized == normalized);
        return agent.ToRef();
    }

    public async Task<Person> PersonAsync(string email)
    {
        await using var session = Store.QuerySession();
        var normalized = Emails.Normalize(email);
        return await session.Query<Person>().SingleAsync(p => p.EmailNormalized == normalized);
    }

    public async ValueTask DisposeAsync()
    {
        await App.StopAsync();
        await App.DisposeAsync();
    }
}

/// <summary>Runs the real migrator's entry point, as the release does (ADR-0002).</summary>
public static class Migrator
{
    public static Task<int> RunAsync(string connectionString, params string[] args) =>
        DbMigrator.Program.Main([$"--ConnectionStrings:Postgres={connectionString}", .. args]);
}
