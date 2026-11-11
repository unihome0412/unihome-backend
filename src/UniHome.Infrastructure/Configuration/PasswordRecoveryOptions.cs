namespace UniHome.Infrastructure.Configuration;

/// <summary>
/// URL base del cliente para el enlace de recuperación. No es un secreto, pero
/// debe configurarse por entorno; el repositorio no incluye una URL de producción.
/// </summary>
public sealed class PasswordRecoveryOptions
{
    public const string SectionName = "PasswordRecovery";
    public const int MaxResetLinkBaseUrlLength = 2048;

    public string? ResetLinkBaseUrl { get; set; }

    public bool HasValidResetLinkBaseUrl => TryValidateBaseUrl(out _);

    public bool TryBuildResetUrl(string token, out string resetUrl)
    {
        resetUrl = string.Empty;

        if (string.IsNullOrWhiteSpace(token) ||
            !TryValidateBaseUrl(out var baseUri))
        {
            return false;
        }

        var baseUrl = baseUri.AbsoluteUri.TrimEnd('/');
        resetUrl = $"{baseUrl}?token={Uri.EscapeDataString(token)}";
        return true;
    }

    private bool TryValidateBaseUrl(out Uri baseUri)
    {
        baseUri = null!;
        var candidate = ResetLinkBaseUrl?.Trim();

        if (string.IsNullOrWhiteSpace(candidate) ||
            candidate.Length > MaxResetLinkBaseUrlLength ||
            candidate.Any(char.IsControl) ||
            !Uri.TryCreate(candidate, UriKind.Absolute, out var parsed) ||
            parsed.UserInfo.Length > 0 ||
            !string.IsNullOrEmpty(parsed.Query) ||
            !string.IsNullOrEmpty(parsed.Fragment) ||
            (parsed.Scheme != Uri.UriSchemeHttps &&
             !(parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback)))
        {
            return false;
        }

        baseUri = parsed;
        return true;
    }
}
