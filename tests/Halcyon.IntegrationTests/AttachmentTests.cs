using System.Net;
using Halcyon.App.Features.Tickets;
using Halcyon.App.Storage;
using Halcyon.Domain;
using Halcyon.Domain.Tickets;
using Halcyon.IntegrationTests.Infrastructure;
using Marten;

namespace Halcyon.IntegrationTests;

/// <summary>Attachments on the report and on the ticket page, and their download (storage in memory).</summary>
public class AttachmentTests(PostgresContainer postgres) : TicketTestBase(postgres)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private InMemoryAttachmentStorage Storage => (InMemoryAttachmentStorage)App.Get<IAttachmentStorage>();

    [Fact]
    public async Task FilesSentWithTheReport_AreOnTheTicket_AndInTheStorage()
    {
        var command = Command(subcategory: "it.application") with
        {
            Files = [new PendingFile("blad.png", Png), new PendingFile("log.txt", "x"u8.ToArray())],
        };

        var reported = await App.Get<ReportTicket>().ByAgentAsync(Anna, command);

        var view = await ViewAsync(reported.TicketId);
        view.Attachments.Select(a => a.FileName).ShouldBe(["blad.png", "log.txt"]);
        view.Attachments[0].ContentType.ShouldBe("image/png");
        view.Attachments[0].AddedBy.ShouldBe("Anna Nowak");
        Storage.Objects[view.Attachments[0].StorageKey].Content.ShouldBe(Png);

        var details = await App.Get<TicketQueries>().DetailsAsync(reported.TicketId);
        details!.Timeline.Count(e => e.Kind == TimelineKind.AttachmentAdded).ShouldBe(2);
    }

    [Fact]
    public async Task OnThePublicForm_TheRequesterIsTheAuthor()
    {
        var command = Command(personId: Adam.Id) with { ReportedAt = null, Files = [new PendingFile("a.png", Png)] };

        var reported = await App.Get<ReportTicket>().SelfServiceAsync(command);

        (await ViewAsync(reported.TicketId)).Attachments.Single().AddedBy.ShouldBe(Adam.FullName);
    }

    [Fact]
    public async Task AFileOfAnotherType_StopsTheReport_AndNothingIsStored()
    {
        var command = Command() with { Files = [new PendingFile("a.png", Png), new PendingFile("virus.exe", Png)] };

        await Should.ThrowAsync<DomainException>(() => App.Get<ReportTicket>().ByAgentAsync(Anna, command));

        Storage.Objects.ShouldBeEmpty();
        await using var session = App.Store.QuerySession();
        (await session.Query<Database.Documents.TicketView>().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AnAgent_AddsFilesToAnExistingTicket()
    {
        var id = await ReportAsync();

        var added = await App.Get<AddTicketAttachments>().ExecuteAsync(id, Piotr, [new PendingFile("umowa.docx", Png)]);

        added.Single().AddedBy.ShouldBe("Piotr Kowalski");
        (await ViewAsync(id)).Attachments.Single().FileName.ShouldBe("umowa.docx");
    }

    [Fact]
    public async Task Download_NeedsSignIn()
    {
        var id = await ReportAsync();
        var added = await App.Get<AddTicketAttachments>().ExecuteAsync(id, Anna, [new PendingFile("a.png", Png)]);
        using var client = App.Client();

        var response = await client.GetAsync(
            new Uri(AttachmentEndpoints.UrlFor(id, added[0].AttachmentId), UriKind.Relative)
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldContain("/logowanie");
    }

    [Fact]
    public async Task Download_ShowsImagesInline_AndDownloadsTheRest_NeverSniffed()
    {
        var id = await ReportAsync();
        var added = await App.Get<AddTicketAttachments>()
            .ExecuteAsync(id, Anna, [new PendingFile("zrzut ekranu.png", Png), new PendingFile("umowa.docx", Png)]);
        using var client = await SignedInClientAsync();

        var image = await client.GetAsync(
            new Uri("/" + AttachmentEndpoints.UrlFor(id, added[0].AttachmentId), UriKind.Relative)
        );
        image.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await image.Content.ReadAsByteArrayAsync()).ShouldBe(Png);
        image.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        image.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        image.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);

        var document = await client.GetAsync(
            new Uri("/" + AttachmentEndpoints.UrlFor(id, added[1].AttachmentId), UriKind.Relative)
        );
        document.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        document.Content.Headers.ContentDisposition.FileNameStar.ShouldBe("umowa.docx");

        var forced = await client.GetAsync(
            new Uri("/" + AttachmentEndpoints.UrlFor(id, added[0].AttachmentId, download: true), UriKind.Relative)
        );
        forced.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");

        (await client.GetAsync(new Uri($"/zalaczniki/{id}/{Guid.NewGuid()}", UriKind.Relative))).StatusCode.ShouldBe(
            HttpStatusCode.NotFound
        );
    }

    private async Task<HttpClient> SignedInClientAsync()
    {
        var client = App.Client();
        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["email"] = "anna.nowak@company.io", ["password"] = TestApp.Password }
        );
        var login = await client.PostAsync(new Uri("/auth/login", UriKind.Relative), form);
        var cookie = login
            .Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("halcyon.session=", StringComparison.Ordinal));
        client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        return client;
    }
}
