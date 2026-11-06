namespace UniHome.Infrastructure.Configuration;

/// <summary>
/// Configuración de emisión y validación de JWT. La clave debe venir de User
/// Secrets o variables de entorno; nunca de un archivo versionado.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinSigningKeyLength = 32;

    public string Issuer { get; set; } = "UniHome";

    public string Audience { get; set; } = "UniHome.Mobile";

    public string? SigningKey { get; set; }

    /// <summary>Vigencia del access token. Fuente sujeta a revisión de seguridad.</summary>
    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>Vigencia del refresh token y de la sesión.</summary>
    public int RefreshTokenDays { get; set; } = 30;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenMinutes);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenDays);

    public bool HasSecureSigningKey =>
        !string.IsNullOrWhiteSpace(SigningKey) &&
        SigningKey.Length >= MinSigningKeyLength;

    /// <summary>
    /// Indica que la clave se generó en memoria para Development y no debe usarse
    /// para validar tokens entre instancias.
    /// </summary>
    public bool EphemeralSigningKey { get; set; }
}
