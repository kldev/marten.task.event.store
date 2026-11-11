using Halcyon.App.Features.Tickets;
using Halcyon.App.Storage;
using Halcyon.IntegrationTests.Infrastructure;

namespace Halcyon.IntegrationTests;

/// <summary>
/// The real S3 client against the same RustFS as compose: the SDK settings (path style, no CRC32 trailers) must work,
/// and the readiness check sees the bucket.
/// </summary>
public class RustFsStorageTests(PostgresContainer postgres, RustFsContainer rustFs)
    : IClassFixture<RustFsContainer>,
        IAsyncLifetime
{
    private TestApp app = null!;

    public async ValueTask InitializeAsync()
    {
        app = await TestApp.StartAsync(postgres, settings: rustFs.Settings);
        await app.SeedPeopleAsync();
    }

    public async ValueTask DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task AFile_GoesToS3_AndComesBack()
    {
        var bytes = Enumerable.Range(0, 300_000).Select(i => (byte)i).ToArray();
        var anna = await app.AgentAsync("anna.nowak@company.io");
        var adam = await app.PersonAsync("adam.mazur@company.io");
        var command = new ReportTicketCommand(
            "Błąd aplikacji",
            "Zrzut w załączniku.",
            "it",
            "it.application",
            Domain.Tickets.Priority.High,
            DateTimeOffset.UtcNow,
            adam.Id,
            false,
            [new PendingFile("blad.png", bytes)]
        );

        var reported = await app.Get<ReportTicket>().ByAgentAsync(anna, command);

        var key = Domain.Tickets.Ticket.StorageKeyFor(reported.TicketId, Guid.Empty)[..^32];
        var details = await app.Get<TicketQueries>().DetailsAsync(reported.TicketId);
        var attachment = details!.Ticket.Attachments.Single();
        attachment.StorageKey.ShouldStartWith(key);
        await using var stored = await app.Get<IAttachmentStorage>()
            .OpenAsync(attachment.StorageKey, CancellationToken.None);
        using var copy = new MemoryStream();
        await stored!.CopyToAsync(copy);
        copy.ToArray().ShouldBe(bytes);
    }

    [Fact]
    public async Task Readiness_SeesTheBucket()
    {
        using var client = app.Client();
        (await client.GetAsync(new Uri("/health/ready", UriKind.Relative))).IsSuccessStatusCode.ShouldBeTrue();
    }
}
