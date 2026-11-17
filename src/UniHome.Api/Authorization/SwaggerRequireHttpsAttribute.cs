using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using UniHome.Api.Configuration;

namespace UniHome.Api.Authorization;

/// <summary>
/// Aplica la redirección HTTPS estándar de ASP.NET Core solo cuando Swagger está
/// habilitado. La comprobación de esquema queda en el filtro del framework, no
/// en una condición de autenticación o antiforgery controlada por la petición.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = true)]
public sealed class SwaggerRequireHttpsAttribute : RequireHttpsAttribute
{
    public override void OnAuthorization(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var apiOptions = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<ApiOptions>>()
            .Value;

        if (!apiOptions.EnableSwagger)
        {
            // Sin Swagger habilitado la acción responde 404; no se redirige una
            // ruta que no debe existir.
            return;
        }

        base.OnAuthorization(context);
    }
}
