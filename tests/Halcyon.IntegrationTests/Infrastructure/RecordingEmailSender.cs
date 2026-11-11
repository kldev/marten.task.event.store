using System.Collections.Concurrent;
using Halcyon.App.Notifications;

namespace Halcyon.IntegrationTests.Infrastructure;

/// <summary>Keeps the mails instead of sending them; can be told to fail the next sends (a flaky SMTP server).</summary>
public sealed class RecordingEmailSender : ISendEmail
{
    private readonly ConcurrentQueue<OutgoingMail> sent = new();
    private int failuresLeft;

    public IReadOnlyList<OutgoingMail> Sent => [.. sent];

    public int Attempts { get; private set; }

    public void FailNext(int times) => failuresLeft = times;

    public Task SendAsync(OutgoingMail mail, CancellationToken ct)
    {
        Attempts++;
        if (Interlocked.Decrement(ref failuresLeft) >= 0)
            throw new IOException("SMTP connection reset (test).");
        sent.Enqueue(mail);
        return Task.CompletedTask;
    }

    /// <summary>The mails to <paramref name="to"/> once there are at least <paramref name="count"/> of them.</summary>
    public async Task<IReadOnlyList<OutgoingMail>> WaitForAsync(string to, int count = 1, TimeSpan? timeout = null)
    {
        var until = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (true)
        {
            var mails = Sent.Where(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase)).ToList();
            if (mails.Count >= count)
                return mails;
            if (DateTime.UtcNow > until)
                throw new TimeoutException($"Expected {count} mail(s) to {to}, got {mails.Count}.");
            await Task.Delay(100);
        }
    }
}
