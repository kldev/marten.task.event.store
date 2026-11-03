using Halcyon.App.Storage;
using Halcyon.Database.Documents;
using Halcyon.Domain;
using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using Marten;
using Microsoft.Net.Http.Headers;

namespace Halcyon.App.Features.Tickets;

/// <summary>
/// A file picked in the browser, already read: Blazor's picker forgets earlier files when the user picks again, so the
/// bytes are kept from the moment of picking (at most <see cref="AttachmentRules.MaxBytes"/> each).
/// </summary>
public sealed record PendingFile(string Name, byte[] Content);

/// <summary>
/// Puts the files in the object storage before the events are saved, and takes them out again when the save fails -
/// so an event never points at a missing file. A file left behind by a crash between the two is only an orphan.
/// </summary>
public sealed class AttachmentFiles(IAttachmentStorage storage, ILogger<AttachmentFiles> logger)
{
    public async Task SaveWithAsync(
        IReadOnlyList<(TicketAttachmentAdded Event, PendingFile File)> files,
        Func<Task> save,
        CancellationToken ct
    )
    {
        var stored = new List<string>();
        try
        {
            foreach (var (e, file) in files)
            {
                using var content = new MemoryStream(file.Content, writable: false);
                await storage.PutAsync(e.StorageKey, content, e.ContentType, ct);
                stored.Add(e.StorageKey);
            }
            await save();
        }
        catch
        {
            foreach (var key in stored)
            {
                try
                {
                    await storage.DeleteAsync(key, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Could not remove an attachment after a failed save: {Key}", key);
                }
            }
            throw;
        }
    }
}

/// <summary>An agent attaches files to an existing ticket, in any state (like a comment).</summary>
public sealed class AddTicketAttachments(IDocumentStore store, AttachmentFiles files, TimeProvider time)
{
    public async Task<IReadOnlyList<TicketAttachmentAdded>> ExecuteAsync(
        Guid ticketId,
        AgentRef agent,
        IReadOnlyList<PendingFile> picked,
        CancellationToken ct = default
    )
    {
        if (picked.Count == 0)
            throw new DomainException("Wybierz plik do dodania.");

        await using var session = store.LightweightSession();
        var stream = await session.Events.FetchForWriting<Ticket>(ticketId, ct);
        var ticket = stream.Aggregate ?? throw new DomainException("Nie ma takiego zgłoszenia.");

        var now = time.GetUtcNow();
        var added = new List<(TicketAttachmentAdded, PendingFile)>();
        foreach (var file in picked)
        {
            var e = ticket.AddAttachment(
                Guid.CreateVersion7(),
                file.Name,
                file.Content.Length,
                agent,
                agent.FullName,
                now
            );
            ticket = Ticket.Evolve(ticket, e);
            added.Add((e, file));
        }

        stream.AppendMany(added.Select(a => (object)a.Item1).ToArray());
        try
        {
            await files.SaveWithAsync(added, () => session.SaveChangesAsync(ct), ct);
        }
        catch (Exception exception) when (TicketWriter.IsConcurrency(exception))
        {
            throw new DomainException("Ktoś zmienił to zgłoszenie w tym samym momencie. Spróbuj ponownie.", exception);
        }
        return added.Select(a => a.Item1).ToList();
    }
}

/// <summary>
/// <c>GET /zalaczniki/{ticket}/{attachment}</c> - for signed-in agents only. Images and PDF open in the browser
/// (<c>?pobierz=1</c> downloads them), the rest is always downloaded; nosniff and a sandbox so a file never runs.
/// </summary>
public static class AttachmentEndpoints
{
    public static string UrlFor(Guid ticketId, Guid attachmentId, bool download = false) =>
        $"zalaczniki/{ticketId}/{attachmentId}" + (download ? "?pobierz=1" : "");

    public static IEndpointRouteBuilder MapAttachments(this IEndpointRouteBuilder app)
    {
        app.MapGet("/zalaczniki/{ticketId:guid}/{attachmentId:guid}", DownloadAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> DownloadAsync(
        Guid ticketId,
        Guid attachmentId,
        string? pobierz,
        HttpContext http,
        IDocumentStore store,
        IAttachmentStorage storage,
        CancellationToken ct
    )
    {
        await using var session = store.QuerySession();
        var view = await session.LoadAsync<TicketView>(ticketId, ct);
        var attachment = view?.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null)
            return Results.NotFound();

        var content = await storage.OpenAsync(attachment.StorageKey, ct);
        if (content is null)
            return Results.NotFound();

        var inline = pobierz is not ("1" or "true") && AttachmentRules.ShowInline(attachment.ContentType);
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(attachment.FileName);
        http.Response.Headers.ContentDisposition = disposition.ToString();
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers.ContentSecurityPolicy = "sandbox";
        http.Response.Headers.CacheControl = new CacheControlHeaderValue
        {
            Private = true,
            MaxAge = TimeSpan.FromHours(1),
        }.ToString();
        return Results.Stream(content, attachment.ContentType);
    }
}
