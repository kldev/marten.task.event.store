using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Halcyon.Database.Documents;
using Halcyon.Domain.People;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace Halcyon.App.Auth;

/// <summary>E-mail of an agent + the shared password (ADR-0005).</summary>
public sealed class SignInService(IDocumentStore store, IOptions<AuthOptions> options, ILogger<SignInService> logger)
{
    public const string AgentIdClaim = ClaimTypes.NameIdentifier;

    public async Task<ClaimsPrincipal?> VerifyAsync(string? email, string? password, CancellationToken ct)
    {
        // Always compare the password, even for an unknown address: the answer takes the same time either way.
        var passwordOk = FixedTimeEquals(password ?? "", options.Value.AgentPassword);

        Agent? agent = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = Emails.Normalize(email);
            await using var session = store.QuerySession();
            agent = await session.Query<Agent>().FirstOrDefaultAsync(a => a.EmailNormalized == normalized, ct);
        }

        if (agent is null || !passwordOk)
        {
            // Never the address or the password in the log (ADR-0005), only why.
            logger.LogInformation("Sign-in refused: {Reason}", agent is null ? "unknown_agent" : "wrong_password");
            return null;
        }

        return PrincipalFor(agent);
    }

    /// <summary>The demo shortcut: the configured default agent, no password. Null when off or the agent is unknown.</summary>
    public async Task<ClaimsPrincipal?> DefaultAgentAsync(CancellationToken ct)
    {
        if (!options.Value.HasDefaultAgent)
        {
            return null;
        }

        var normalized = Emails.Normalize(options.Value.DefaultAgentEmail!);
        await using var session = store.QuerySession();
        var agent = await session.Query<Agent>().FirstOrDefaultAsync(a => a.EmailNormalized == normalized, ct);
        if (agent is null)
        {
            logger.LogWarning("Default agent sign-in refused: Auth:DefaultAgentEmail is not an agent");
            return null;
        }

        return PrincipalFor(agent);
    }

    private static ClaimsPrincipal PrincipalFor(Agent agent)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(AgentIdClaim, agent.Id.ToString()),
                new Claim(ClaimTypes.Name, agent.FullName),
                new Claim(ClaimTypes.Email, agent.Email),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme
        );
        return new ClaimsPrincipal(identity);
    }

    private static bool FixedTimeEquals(string given, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(given)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected))
        );
}
