namespace UniHome.Api.Configuration;

/// <summary>
/// Límites provisionales de intentos por IP. Los umbrales definitivos requieren
/// aprobación de seguridad y pueden ajustarse por configuración.
/// </summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "Auth:RateLimit";

    public int LoginPermitLimit { get; set; } = 10;

    public int LoginWindowSeconds { get; set; } = 60;

    public int RefreshPermitLimit { get; set; } = 20;

    public int RefreshWindowSeconds { get; set; } = 60;
}

public static class AuthRateLimitPolicies
{
    public const string Login = "auth-login";
    public const string Refresh = "auth-refresh";
}
