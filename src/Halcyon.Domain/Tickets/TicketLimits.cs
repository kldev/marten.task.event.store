namespace Halcyon.Domain.Tickets;

public static class TicketLimits
{
    public const int TitleMax = 200;
    public const int DescriptionMax = 4000;
    public const int ReasonMax = 500;
    public const int CommentMax = 2000;

    /// <summary>How far ahead a report date may be: clocks of two machines are never exactly the same.</summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    /// <summary>How far back an agent may date a report (a phone call this morning, a mail from last week).</summary>
    public static readonly TimeSpan MaxBackdating = TimeSpan.FromDays(30);

    /// <summary>Under this much time left a ticket is "due soon" (warning).</summary>
    public static readonly TimeSpan DueSoonThreshold = TimeSpan.FromHours(1);

    internal static string Required(string? value, string field, int max)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new DomainException($"Pole „{field}” jest wymagane.");
        if (trimmed.Length > max)
            throw new DomainException($"Pole „{field}” może mieć najwyżej {max} znaków.");
        return trimmed;
    }

    internal static string? Optional(string? value, string field, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > max)
            throw new DomainException($"Pole „{field}” może mieć najwyżej {max} znaków.");
        return trimmed;
    }
}
