namespace Halcyon.Domain.ValueObjects;

/// <summary>What every free-text value object does first: trim, then required or not, then not too long.</summary>
internal static class TextRules
{
    public static (string? value, string? error) Required(
        string? value,
        int maxLength,
        string requiredMessage,
        string tooLongMessage
    )
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
            return (null, requiredMessage);
        return trimmed.Length > maxLength ? (null, tooLongMessage) : (trimmed, null);
    }

    /// <returns>No value and no error for a blank one: not giving it is allowed.</returns>
    public static (string? value, string? error) Optional(string? value, int maxLength, string tooLongMessage)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return (null, null);
        return trimmed.Length > maxLength ? (null, tooLongMessage) : (trimmed, null);
    }

    /// <summary>"Pole „{field}” …" - the refusals of a report field name its label (spec 008).</summary>
    public static string Field(string field, string rest) => $"Pole „{field}” {rest}";
}
