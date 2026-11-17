using Microsoft.AspNetCore.Authorization;

namespace UniHome.Api.Authorization;

public sealed class DocumentationAccessRequirement : IAuthorizationRequirement;

/// <summary>
/// Autoriza la documentación si el sujeto es administrador o lector documental
/// configurado. No concede ninguna otra ruta administrativa.
/// </summary>
public sealed class DocumentationAccessHandler
    : AuthorizationHandler<DocumentationAccessRequirement>
{
    private readonly DocumentationAccessEvaluator _evaluator;

    public DocumentationAccessHandler(DocumentationAccessEvaluator evaluator)
    {
        _evaluator = evaluator;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DocumentationAccessRequirement requirement)
    {
        if (_evaluator.IsAllowed(context.User.FindFirst("sub")?.Value))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
