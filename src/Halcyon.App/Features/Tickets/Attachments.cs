using Halcyon.App.Storage;
using Halcyon.Database.Documents;
using Halcyon.Domain;
using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using Marten;
using Microsoft.Net.Http.Headers;
using Wolverine;

namespace Halcyon.App.Features.Tickets;

/// <summary>
/// A file picked in the browser, already read: Blazor's picker forgets earlier files when the user picks again, so the
/// bytes are kept from the moment of picking (at most <see cref="AttachmentRules.MaxBytes"/> each).
/// </summary>
public sealed record PendingFile(string Name, byte[] Content);

/// <summary>A file the command attaches: its id (and so its storage key) is chosen before the bytes are stored.</summary>
public sealed record NewAttachment(Guid AttachmentId, string FileName, long Size);

/// <summary>
/// Puts the files in the object storage before the command saves its events, and takes them out again when the save
/// fails - so an event never points at a missing file (ADR-0008). A file left behind by a crash between the two is only
/// an orphan.
/// </summary>
public sealed class AttachmentFiles(IAttachmentStorage storage, ILogger<AttachmentFiles> logger)
{
    /// <summary>
    /// Checks every file first (nothing is stored when one is refused), stores them under
    /// <see cref="Ticket.StorageKeyFor"/>, then runs <paramref name="save"/> with what the command attaches.
    /// </summary>
    public async Task<T> SaveWithAsync<T>(
        Guid ticketId,
        IReadOnlyList<PendingFile> files,
        Func<IReadOnlyList<NewAttachment>, Task<T>> save,
        CancellationToken ct
    )
    {
        var planned = files
            .Select(file =>
            {
                var (name, type) = AttachmentRules.Check(file.Name, file.Content.Length);
                var attachment = new NewAttachment(Guid.CreateVersion7(), file.Name, file.Content.Length);
                return (
                    Attachment: attachment,
                    Key: Ticket.StorageKeyFor(ticketId, attachment.AttachmentId),
                    Name: name,
                    Type: type,
                    File: file
                );
            })
            .ToList();

        var stored = new List<string>();
        try
        {
            foreach (var p in planned)
            {
                using var content = new MemoryStream(p.File.Content, writable: false);
                await storage.PutAsync(p.Key, content, p.Type, ct);
                stored.Add(p.Key);
            }
            return await save([.. planned.Select(p => p.Attachment)]);
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
public sealed class AddTicketAttachments(TicketCommands commands, AttachmentFiles files)
{
    private const string NothingPicked = "Wybierz plik do dodania.";

    public async Task<IReadOnlyList<TicketAttachmentAdded>> ExecuteAsync(
        Guid ticketId,
        AgentRef agent,
        IReadOnlyList<PendingFile> picked,
        CancellationToken ct = default
    )
    {
        if (picked.Count == 0)
            throw new DomainException(NothingPicked);
        var added = await files.SaveWithAsync(
            ticketId,
            picked,
            attachments =>
                commands.SendAsync<AttachmentsAdded>(
                    ticketId,
                    new AddTicketAttachmentsCommand(ticketId, agent, attachments),
                    ct
                ),
            ct
        );
        return added?.Events ?? [];
    }
}

public sealed record AddTicketAttachmentsCommand(Guid TicketId, AgentRef Agent, IReadOnlyList<NewAttachment> Files);

/// <summary>The handler's answer: one event per file (a list is not an event, so it is wrapped).</summary>
public sealed record AttachmentsAdded(IReadOnlyList<TicketAttachmentAdded> Events);

public static class AddTicketAttachmentsHandler
{
    public static async Task<(AttachmentsAdded, OutgoingMessages)> Handle(
        AddTicketAttachmentsCommand command,
        IDocumentSession session,
        TimeProvider time,
        CancellationToken ct
    )
    {
        var (stream, ticket) = await TicketStreams.LockAsync(session, command.TicketId, ct);
        var now = time.GetUtcNow();
        var events = new List<TicketAttachmentAdded>();
        foreach (var file in command.Files)
        {
            var e = ticket.AddAttachment(
                file.AttachmentId,
                file.FileName,
                file.Size,
                command.Agent,
                command.Agent.FullName,
                now
            );
            ticket = Ticket.Evolve(ticket, e);
            events.Add(e);
        }
        stream.AppendMany([.. events]);
        return (new AttachmentsAdded(events), []);
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
