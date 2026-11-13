using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using UniHome.Api.Configuration;

namespace UniHome.Api.Authorization;

public sealed class AdminAccessRequirement : IAuthorizationRequirement;

/// <summary>
/// Autoriza solo si el sujeto del JWT validado pertenece al privilegio
/// administrativo configurado. No confía en claims de rol del cliente.
/// </summary>
public sealed class AdminAccessHandler : AuthorizationHandler<AdminAccessRequirement>
{
    private readonly IOptions<AdminAccessOptions> _options;

    public AdminAccessHandler(IOptions<AdminAccessOptions> options)
    {
        _options = options;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminAccessRequirement requirement)
    {
        var subject = context.User.FindFirst("sub")?.Value;

        if (_options.Value.IsAdmin(subject))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
