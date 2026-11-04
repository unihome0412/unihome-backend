namespace UniHome.Infrastructure.Configuration;

/// <summary>
/// Configuración de la protección HMAC de códigos de confirmación.
/// El secreto se obtiene de User Secrets o variables de entorno; nunca del repositorio.
/// </summary>
public sealed class VerificationCodeOptions
{
    public const string SectionName = "VerificationCode";

    public const int MinSecretLength = 32;

    public string? HmacSecret { get; set; }

    public bool HasStrongSecret =>
        !string.IsNullOrWhiteSpace(HmacSecret) &&
        HmacSecret.Length >= MinSecretLength;
}
