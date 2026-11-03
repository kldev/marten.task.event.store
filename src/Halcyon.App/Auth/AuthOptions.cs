using System.ComponentModel.DataAnnotations;

namespace Halcyon.App.Auth;

/// <summary>
/// Sign-in settings (ADR-0005). The shared password comes only from the environment (<c>Auth__AgentPassword</c>,
/// in compose <c>AGENT_PASSWORD</c> from <c>.env</c>) - never from a file in the repository.
/// </summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    [Required(ErrorMessage = "Auth:AgentPassword is required (env Auth__AgentPassword / AGENT_PASSWORD in .env).")]
    [MinLength(4, ErrorMessage = "Auth:AgentPassword must have at least 4 characters.")]
    public string AgentPassword { get; set; } = "";

    /// <summary>
    /// Demo shortcut: when set, the sign-in page shows "Zaloguj jako domyślny agent", which signs in as this agent
    /// without the password (<c>Auth__DefaultAgentEmail</c>, in compose <c>DEFAULT_AGENT_EMAIL</c>). Empty = off.
    /// </summary>
    public string? DefaultAgentEmail { get; set; }

    public bool HasDefaultAgent => !string.IsNullOrWhiteSpace(DefaultAgentEmail);

    [Range(1, 24 * 30)]
    public int SessionHours { get; set; } = 8;

    /// <summary>Sign-in attempts per minute from one address (brute-force brake).</summary>
    [Range(1, 10_000)]
    public int SignInAttemptsPerMinute { get; set; } = 10;
}
