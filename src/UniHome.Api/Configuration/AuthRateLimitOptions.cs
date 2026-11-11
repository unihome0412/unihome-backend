namespace UniHome.Api.Configuration;

/// <summary>
/// Límites provisionales de intentos por IP. Los umbrales definitivos requieren
/// aprobación de seguridad y pueden ajustarse por configuración.
/// </summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "Auth:RateLimit";

    /// <summary>
    /// Sal criptográfica para seudonimizar las IPs antes de usarlas como clave en Redis.
    /// Previene ataques de diccionario y búsqueda inversa sobre las direcciones de los clientes.
    /// Se configura vía Auth__RateLimit__IpHashSalt.
    /// </summary>
    public string IpHashSalt { get; set; } = string.Empty;

    public int LoginPermitLimit { get; set; } = 10;

    public int LoginWindowSeconds { get; set; } = 60;

    public int RefreshPermitLimit { get; set; } = 20;

    public int RefreshWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Límite por IP de EP-M01-02. La spec exige "límite de intentos por cuenta e
    /// IP" sin fijar valores; el límite por cuenta lo aplica el dominio
    /// (<see cref="UniHome.Domain.Accounts.VerificationCode.MaxConfirmationAttempts"/>).
    /// Este es el valor provisional por IP.
    /// </summary>
    public int ConfirmEmailPermitLimit { get; set; } = 10;

    public int ConfirmEmailWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Límite por IP de EP-M01-03 (429). Más estricto que el de confirmación
    /// porque cada solicitud aceptada puede disparar un envío de correo.
    /// </summary>
    public int ResendPermitLimit { get; set; } = 5;

    public int ResendWindowSeconds { get; set; } = 60;
}

public static class AuthRateLimitPolicies
{
    public const string Login = "auth-login";
    public const string Refresh = "auth-refresh";
    public const string ConfirmEmail = "auth-confirm-email";
    public const string Resend = "auth-confirm-email-resend";
}
