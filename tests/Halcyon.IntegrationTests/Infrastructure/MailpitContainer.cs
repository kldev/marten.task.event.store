using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Halcyon.IntegrationTests.Infrastructure;

/// <summary>Mailpit (the same as in compose): a real SMTP server whose API says what arrived (spec 013).</summary>
// ReSharper disable once ClassNeverInstantiated.Global - xUnit creates it for IClassFixture<MailpitContainer>.
public sealed class MailpitContainer : IAsyncLifetime
{
    private const string Image = "axllent/mailpit:v1.31.6";

    private readonly IContainer container = new ContainerBuilder(Image)
        .WithPortBinding(1025, assignRandomHostPort: true)
        .WithPortBinding(8025, assignRandomHostPort: true)
        .WithWaitStrategy(
            Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/api/v1/messages"))
        )
        .Build();

    private readonly HttpClient http = new();

    public Dictionary<string, string?> Settings =>
        new()
        {
            ["Mail:Provider"] = "Smtp",
            ["Mail:BaseUrl"] = "https://halcyon.test",
            ["Mail:Smtp:Host"] = container.Hostname,
            ["Mail:Smtp:Port"] = container
                .GetMappedPublicPort(1025)
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Mail:Smtp:Security"] = "None",
        };

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
        http.BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(8025)}");
    }

    /// <summary>The messages to <paramref name="to"/>, waiting until at least <paramref name="count"/> arrived.</summary>
    public async Task<IReadOnlyList<MailpitMessage>> WaitForAsync(string to, int count = 1)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var list = await http.GetFromJsonAsync<MailpitList>("/api/v1/messages?limit=200");
            var mine = (list?.Messages ?? [])
                .Where(m => m.To.Any(a => string.Equals(a.Address, to, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (mine.Count >= count)
                return mine;
            if (DateTime.UtcNow > until)
                throw new TimeoutException($"Expected {count} mail(s) to {to} in Mailpit, got {mine.Count}.");
            await Task.Delay(200);
        }
    }

    public async Task<string> TextOfAsync(MailpitMessage message) =>
        (await http.GetFromJsonAsync<MailpitBody>($"/api/v1/message/{message.Id}"))?.Text ?? "";

    public async ValueTask DisposeAsync()
    {
        http.Dispose();
        await container.DisposeAsync();
    }

    public sealed record MailpitList([property: JsonPropertyName("messages")] IReadOnlyList<MailpitMessage> Messages);

    // ReSharper disable once ClassNeverInstantiated.Global - System.Text.Json creates it from Mailpit's answer.
    public sealed record MailpitMessage(
        [property: JsonPropertyName("ID")] string Id,
        [property: JsonPropertyName("Subject")] string Subject,
        [property: JsonPropertyName("From")] MailpitAddress From,
        [property: JsonPropertyName("To")] IReadOnlyList<MailpitAddress> To
    );

    // ReSharper disable once ClassNeverInstantiated.Global - System.Text.Json creates it from Mailpit's answer.
    public sealed record MailpitAddress(
        [property: JsonPropertyName("Name")] string Name,
        [property: JsonPropertyName("Address")] string Address
    );

    public sealed record MailpitBody([property: JsonPropertyName("Text")] string Text);
}
