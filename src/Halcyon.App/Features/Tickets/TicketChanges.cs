namespace Halcyon.App.Features.Tickets;

/// <summary>
/// Per circuit: a page that changed a ticket says so, and the header counters ask the database again at once instead of
/// waiting for the next clock tick.
/// </summary>
public sealed class TicketChanges
{
    public event Func<Task>? Changed;

    public async Task NotifyAsync()
    {
        if (Changed is { } handlers)
            foreach (var handler in handlers.GetInvocationList().Cast<Func<Task>>())
                await handler();
    }
}
