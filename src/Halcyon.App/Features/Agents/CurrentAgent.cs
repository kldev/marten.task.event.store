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
        // Read again on every call and never kept, so it cannot go stale: the event BL0013 asks for has nothing to refresh.
#pragma warning disable BL0013
        var state = await authentication.GetAuthenticationStateAsync();
#pragma warning restore BL0013
        var user = state.User;
        if (
            user.Identity?.IsAuthenticated != true
            || !Guid.TryParse(user.FindFirst(SignInService.AgentIdClaim)?.Value, out var id)
        )
            throw new DomainException("Sesja wygasła - zaloguj się ponownie.");

        return new AgentRef(id, user.Identity.Name ?? "");
    }
}
