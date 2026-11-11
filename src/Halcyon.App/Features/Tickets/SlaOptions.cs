using System.ComponentModel.DataAnnotations;
using Halcyon.Domain.Tickets;

namespace Halcyon.App.Features.Tickets;

/// <summary>
/// The SLA windows in hours (ADR-0013), from the environment (<c>Sla__CriticalHours</c>, ...). The defaults are the
/// owner's 3 h / 8 h / 3 days / 1 week. A wrong set stops the host at start, naming the reason.
/// </summary>
public sealed class SlaOptions : IValidatableObject
{
    public const string Section = "Sla";
    private const int MaxHours = 24 * 366;

    [Range(1, MaxHours)]
    public int CriticalHours { get; set; } = 3;

    [Range(1, MaxHours)]
    public int HighHours { get; set; } = 8;

    [Range(1, MaxHours)]
    public int MediumHours { get; set; } = 72;

    [Range(1, MaxHours)]
    public int LowHours { get; set; } = 168;

    public SlaPolicy ToPolicy() =>
        new(
            TimeSpan.FromHours(CriticalHours),
            TimeSpan.FromHours(HighHours),
            TimeSpan.FromHours(MediumHours),
            TimeSpan.FromHours(LowHours)
        );

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ToPolicy().Problem() is { } problem)
            yield return new ValidationResult($"Sla: {problem}");
    }
}

public static class SlaSetup
{
    public static WebApplicationBuilder AddHalcyonSla(this WebApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<SlaOptions>()
            .Bind(builder.Configuration.GetSection(SlaOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        // An instance, not a factory: the handlers take the policy, and Wolverine's generated code needs a plain
        // registration (ADR-0016). Wrong windows still stop the host at start (ValidateOnStart above).
        var options = builder.Configuration.GetSection(SlaOptions.Section).Get<SlaOptions>() ?? new SlaOptions();
        builder.Services.AddSingleton(options.ToPolicy());
        return builder;
    }
}
