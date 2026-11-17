namespace UniHome.Api.Configuration;

/// <summary>
/// Acceso a la documentación interna. Swagger UI y el documento OpenAPI nunca
/// son anónimos: exigen el privilegio administrativo existente. La sesión de
/// navegador es una cookie HttpOnly de corta vida emitida solo a partir de un
/// Bearer JWT administrativo válido; no se guardan usuarios ni tokens en
/// configuración.
/// </summary>
public sealed class SwaggerAccessOptions
{
    public const string SectionName = "SwaggerAccess";
    public const string PolicyName = "SwaggerAccess";
    public const string CookieName = "__Host-unihome-swagger";
    public const string AuthenticationScheme = "SwaggerCookie";
    public const string PolicyScheme = "UniHomeAuth";

    public const int DefaultLifetimeMinutes = 15;
    public const int MinLifetimeMinutes = 1;
    public const int MaxLifetimeMinutes = 60;

    public int CookieLifetimeMinutes { get; set; } = DefaultLifetimeMinutes;

    public TimeSpan Lifetime => TimeSpan.FromMinutes(
        CookieLifetimeMinutes is >= MinLifetimeMinutes and <= MaxLifetimeMinutes
            ? CookieLifetimeMinutes
            : DefaultLifetimeMinutes);

    public bool IsValid() =>
        CookieLifetimeMinutes is >= MinLifetimeMinutes and <= MaxLifetimeMinutes;

    /// <summary>
    /// Rutas que pueden autenticarse con la cookie de documentación. La cookie
    /// no se acepta en /api/*: para operar la API se sigue exigiendo Bearer.
    /// </summary>
    public static bool IsDocumentationPath(PathString path) =>
        path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase) ||
        path == "/";
}
