using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using Amazon.Runtime;
using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Halcyon.Database;
using Halcyon.Database.Documents;
using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using Halcyon.Domain.ValueObjects;
using Halcyon.E2E.Infrastructure;
using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(E2EEnvironment))]

namespace Halcyon.E2E.Infrastructure;

/// <summary>
/// The whole thing, as a release runs it: PostgreSQL 18 and RustFS (Testcontainers), the migrator with the seed, then the
/// app as its own process with schema changes off - and a Chromium for the tests.
/// </summary>
public sealed class E2EEnvironment : IAsyncLifetime
{
    public const string Password = "e2e-password";

    private const string S3Key = "halcyon-e2e";
    private const string S3Secret = "halcyon-e2e-secret";
    private const string Bucket = "halcyon-attachments";

    /// <summary>
    /// The app runs with a Low window of 5 days instead of 1 week (spec 012, ADR-0013): every page that names an SLA
    /// must read the configuration. The seed, made by the migrator, keeps the default windows in its events.
    /// </summary>
    public const int LowSlaHours = 120;

    private static readonly SlaPolicy Sla = SlaPolicy.Default with { Low = TimeSpan.FromHours(LowSlaHours) };

    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18").Build();
    private readonly IContainer rustFs = new ContainerBuilder("rustfs/rustfs:1.0.0")
        .WithEnvironment("RUSTFS_ACCESS_KEY", S3Key)
        .WithEnvironment("RUSTFS_SECRET_KEY", S3Secret)
        .WithEnvironment("RUSTFS_VOLUMES", "/data")
        .WithEnvironment("RUSTFS_ADDRESS", ":9000")
        .WithPortBinding(9000, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(9000).ForPath("/health")))
        .Build();

    // Mailpit, as in compose: the app sends its mails there over SMTP and the tests read them back (spec 013).
    private readonly IContainer mailpit = new ContainerBuilder("axllent/mailpit:v1.31.6")
        .WithPortBinding(1025, assignRandomHostPort: true)
        .WithPortBinding(8025, assignRandomHostPort: true)
        .WithWaitStrategy(
            Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/api/v1/messages"))
        )
        .Build();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private AmazonS3Client? s3;
    private readonly StringBuilder appLog = new();
    private Process? app;
    private ServiceProvider? services;

    public string BaseUrl { get; private set; } = "";
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IDocumentStore Store => services!.GetRequiredService<IDocumentStore>();
    private string S3Url => $"http://{rustFs.Hostname}:{rustFs.GetMappedPublicPort(9000)}";

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(postgres.StartAsync(), rustFs.StartAsync(), mailpit.StartAsync());
        var connectionString = postgres.GetConnectionString();
        s3 = new AmazonS3Client(
            new BasicAWSCredentials(S3Key, S3Secret),
            new AmazonS3Config
            {
                ServiceURL = S3Url,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
            }
        );
        await s3.PutBucketAsync(Bucket);

        var migrated = await DbMigrator.Program.Main([$"--ConnectionStrings:Postgres={connectionString}", "seed"]);
        migrated.ShouldBe(0, "the migrator (with seed) must succeed");

        services = new ServiceCollection()
            .AddLogging()
            .AddHalcyonDatabase(connectionString, AutoCreate.None, wolverine: false)
            .BuildServiceProvider();

        StartApp(connectionString);
        await WaitUntilHealthyAsync();

        // Installs the browser once (no-op when it is there already).
        var installed = Program.Main(["install", "chromium"]);
        installed.ShouldBe(0, "playwright install chromium");
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(
            new() { Headless = Environment.GetEnvironmentVariable("HEADED") != "1" }
        );
    }

    private void StartApp(string connectionString)
    {
        var appDir = Path.Combine(RepoRoot(), "src", "Halcyon.App");
        // The app's own build output (…/bin/<Configuration>/net10.0), built with this test project's configuration:
        // there sit its runtimeconfig and the static web assets manifest.
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = output.Parent!.Name;
        var dll = Path.Combine(appDir, "bin", configuration, output.Name, "Halcyon.App.dll");
        var port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var start = new ProcessStartInfo("dotnet", [dll, "--urls", BaseUrl])
        {
            WorkingDirectory = appDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            Environment =
            {
                // Development only for the static web assets of a build output (no publish); the database rules are a
                // release's.
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ConnectionStrings__Postgres"] = connectionString,
                ["Auth__AgentPassword"] = Password,
                ["Database__ApplyChangesOnStartup"] = "false",
                ["Storage__ServiceUrl"] = S3Url,
                ["Storage__AccessKey"] = S3Key,
                ["Storage__SecretKey"] = S3Secret,
                ["Storage__Bucket"] = Bucket,
                // Every test signs in, often twice: more than the default 10 attempts a minute from one address.
                ["Auth__SignInAttemptsPerMinute"] = "1000",
                ["Sla__LowHours"] = LowSlaHours.ToString(System.Globalization.CultureInfo.InvariantCulture),
                // Mail over real SMTP to Mailpit (spec 013), as in compose.
                ["Mail__Provider"] = "Smtp",
                ["Mail__BaseUrl"] = BaseUrl,
                ["Mail__Smtp__Host"] = mailpit.Hostname,
                ["Mail__Smtp__Port"] = mailpit
                    .GetMappedPublicPort(1025)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Mail__Smtp__Security"] = "None",
            },
        };

        app = Process.Start(start) ?? throw new InvalidOperationException("Could not start Halcyon.App.");
        app.OutputDataReceived += (_, e) => Log(e.Data);
        app.ErrorDataReceived += (_, e) => Log(e.Data);
        app.BeginOutputReadLine();
        app.BeginErrorReadLine();
    }

    private void Log(string? line)
    {
        if (line is null)
            return;
        lock (appLog)
            appLog.AppendLine(line);
    }

    private async Task WaitUntilHealthyAsync()
    {
        var until = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < until)
        {
            if (app!.HasExited)
                break;
            try
            {
                var response = await Http.GetAsync(new Uri($"{BaseUrl}/health/ready"));
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(300);
        }
        lock (appLog)
            throw new InvalidOperationException($"Halcyon.App did not become ready:{Environment.NewLine}{appLog}");
    }

    public async ValueTask DisposeAsync()
    {
        // The app's own log next to the test output: the first place to look when a page did not render.
        lock (appLog)
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "halcyon-app.log"), appLog.ToString());
        if (browser is not null)
            await browser.DisposeAsync();
        playwright?.Dispose();
        if (app is { HasExited: false })
        {
            app.Kill(entireProcessTree: true);
            await app.WaitForExitAsync();
        }
        app?.Dispose();
        if (services is not null)
            await services.DisposeAsync();
        s3?.Dispose();
        await postgres.DisposeAsync();
        await rustFs.DisposeAsync();
        await mailpit.DisposeAsync();
    }

    /// <summary>A signed-in agent in a fresh browser context (own cookies).</summary>
    public async Task<IPage> SignInAsync(string email = "anna.nowak@company.io")
    {
        var context = await browser!.NewContextAsync(
            new()
            {
                BaseURL = BaseUrl,
                Locale = "pl-PL",
                TimezoneId = "Europe/Warsaw",
            }
        );
        var page = await context.NewPageAsync();
        await page.GotoAsync("/logowanie");
        await page.GetByTestId("signin-email").FillAsync(email);
        await page.GetByTestId("signin-password").FillAsync(Password);
        await page.GetByTestId("signin-submit").ClickAsync();
        await Expect(page.GetByTestId("alert-counters")).ToBeVisibleAsync();
        return page;
    }

    public async Task<IPage> AnonymousPageAsync()
    {
        var context = await browser!.NewContextAsync(
            new()
            {
                BaseURL = BaseUrl,
                Locale = "pl-PL",
                TimezoneId = "Europe/Warsaw",
            }
        );
        return await context.NewPageAsync();
    }

    /// <summary>
    /// Arranges a ticket with the domain's own decision, straight into the store (for times a browser cannot fake). It
    /// goes around the app's bus, so it sends no mail - the mail tests report through the pages.
    /// </summary>
    public async Task<(Guid Id, string Number)> ReportAsync(
        string title,
        Priority priority,
        TimeSpan ago,
        string subcategory = "it.hardware",
        string description = "Przygotowane przez test e2e."
    )
    {
        var anna = await AgentAsync("anna.nowak@company.io");
        await using var session = Store.LightweightSession();
        var person = await session.Query<Person>().FirstAsync(p => p.EmailNormalized == "adam.mazur@company.io");
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var reported = Ticket.Report(
            id,
            new TicketDraft(
                title,
                description,
                subcategory.Split('.')[0],
                subcategory,
                priority,
                now - ago,
                Requester.For(person.ToRef()),
                ReportChannel.Agent,
                anna
            ),
            now,
            sla: Sla
        );
        session.Events.StartStream<Ticket>(id, reported);
        await session.SaveChangesAsync();
        return (id, TicketNumber.For(id).Value);
    }

    /// <summary>A ticket's list row, read straight from the database.</summary>
    public async Task<TicketView> ViewAsync(Guid ticketId)
    {
        await using var session = Store.QuerySession();
        return (await session.LoadAsync<TicketView>(ticketId))!;
    }

    public async Task<AgentRef> AgentAsync(string email)
    {
        await using var session = Store.QuerySession();
        var normalized = Email.Normalize(email);
        return (await session.Query<Agent>().FirstAsync(a => a.EmailNormalized == normalized)).ToRef();
    }

    public async Task TakeAsync(Guid ticketId, string agentEmail)
    {
        var agent = await AgentAsync(agentEmail);
        await using var session = Store.LightweightSession();
        var stream = await session.Events.FetchForWriting<Ticket>(ticketId);
        stream.AppendOne(stream.Aggregate!.Take(agent, DateTimeOffset.UtcNow));
        await session.SaveChangesAsync();
    }

    /// <summary>The mails Mailpit got for <paramref name="to"/>, waiting until one has a subject with <paramref name="subject"/>.</summary>
    public async Task<MailpitMail> MailAsync(string to, string subject)
    {
        using var http = new HttpClient();
        http.BaseAddress = new Uri($"http://{mailpit.Hostname}:{mailpit.GetMappedPublicPort(8025)}");
        var until = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var list = await http.GetFromJsonAsync<MailpitList>("/api/v1/messages?limit=500");
            var found = list?.Messages.FirstOrDefault(m =>
                m.Subject.Contains(subject, StringComparison.Ordinal)
                && m.To.Any(a => string.Equals(a.Address, to, StringComparison.OrdinalIgnoreCase))
            );
            if (found is not null)
            {
                var body = await http.GetFromJsonAsync<MailpitBody>($"/api/v1/message/{found.Id}");
                return new MailpitMail(found.Subject, body?.Text ?? "");
            }
            if (DateTime.UtcNow > until)
                throw new TimeoutException($"No mail to {to} with \"{subject}\" in Mailpit.");
            await Task.Delay(200);
        }
    }

    public sealed record MailpitMail(string Subject, string Text);

    private sealed record MailpitList([property: JsonPropertyName("messages")] IReadOnlyList<MailpitMessage> Messages);

    // ReSharper disable once ClassNeverInstantiated.Local - System.Text.Json creates it from Mailpit's answer.
    private sealed record MailpitMessage(
        [property: JsonPropertyName("ID")] string Id,
        [property: JsonPropertyName("Subject")] string Subject,
        [property: JsonPropertyName("To")] IReadOnlyList<MailpitAddress> To
    );

    // ReSharper disable once ClassNeverInstantiated.Local - System.Text.Json creates it from Mailpit's answer.
    private sealed record MailpitAddress([property: JsonPropertyName("Address")] string Address);

    private sealed record MailpitBody([property: JsonPropertyName("Text")] string Text);

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Halcyon.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Halcyon.slnx not found above the test output.");
    }

    /// <summary>
    /// Types into the list's search box and waits for the row: with the seed's 200+ tickets the lists are paged
    /// (spec 008), so a test finds its ticket the way an agent would. An empty text clears the search.
    /// </summary>
    public static async Task<ILocator> SearchAsync(IPage page, string text)
    {
        var row = page.GetByTestId("ticket-row").Filter(new() { HasText = text });
        // Right after a click in the menu the old list may still be on the screen and take the text; type again then.
        for (var attempt = 1; ; attempt++)
        {
            await page.GetByTestId("filter-search").FillAsync(text);
            if (text.Length == 0)
                return row;
            try
            {
                await Expect(row.First).ToBeVisibleAsync(new() { Timeout = attempt < 3 ? 1500 : 5000 });
                return row;
            }
            catch (PlaywrightException) when (attempt < 3) { }
        }
    }

    public static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..6]}";
}
