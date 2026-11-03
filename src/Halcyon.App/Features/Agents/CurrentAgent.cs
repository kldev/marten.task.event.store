using Halcyon.App.Auth;
using Halcyon.Domain;
using Halcyon.Domain.People;
using Microsoft.AspNetCore.Components.Authorization;

namespace Halcyon.App.Features.Agents;

/// <summary>The signed-in agent of this circuit, as events remember them.</summary>
public sealed class CurrentAgent(AuthenticationStateProvider authentication)
{
    public async Task<AgentRef> GetAsync()
    {
        var state = await authentication.GetAuthenticationStateAsync();
        var user = state.User;
        if (
            user.Identity?.IsAuthenticated != true
            || !Guid.TryParse(user.FindFirst(SignInService.AgentIdClaim)?.Value, out var id)
        )
            throw new DomainException("Sesja wygasła - zaloguj się ponownie.");

        return new AgentRef(id, user.Identity.Name ?? "");
    }
}
