using System.Collections.Concurrent;
using Halcyon.App.Storage;

namespace Halcyon.IntegrationTests.Infrastructure;

/// <summary>Attachments in a dictionary: the default in integration tests; the real S3 is covered by RustFsStorageTests.</summary>
public sealed class InMemoryAttachmentStorage : IAttachmentStorage
{
    public ConcurrentDictionary<string, (byte[] Content, string ContentType)> Objects { get; } = new();

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        Objects[key] = (buffer.ToArray(), contentType);
    }

    public Task<Stream?> OpenAsync(string key, CancellationToken ct) =>
        Task.FromResult<Stream?>(
            Objects.TryGetValue(key, out var o) ? new MemoryStream(o.Content, writable: false) : null
        );

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        Objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task CheckAsync(CancellationToken ct) => Task.CompletedTask;
}
