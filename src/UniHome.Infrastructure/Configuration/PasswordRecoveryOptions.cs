using System.Text.RegularExpressions;

namespace UniHome.Infrastructure.Configuration;

/// <summary>
/// URL base del cliente para el enlace de recuperación. No es un secreto, pero
/// debe configurarse por entorno; el repositorio no incluye una URL de producción.
/// Para Android se puede optar por un App Link HTTPS (recomendado) o por un
/// esquema propio declarado explícitamente en <see cref="AndroidAppScheme"/>.
/// </summary>
public sealed partial class PasswordRecoveryOptions
{
    public const string SectionName = "PasswordRecovery";
    public const int MaxResetLinkBaseUrlLength = 2048;

    private static readonly HashSet<string> ForbiddenSchemes = new(
        [
            "javascript", "data", "file", "blob", "about", "vbscript",
            "content", "intent", "android-app"
        ],
        StringComparer.OrdinalIgnoreCase);

    public string? ResetLinkBaseUrl { get; set; }

    /// <summary>
    /// Esquema Android opcional (por ejemplo <c>unihome</c>) que se acepta en
    /// <see cref="ResetLinkBaseUrl"/> además de HTTPS. No se hardcodea: debe
    /// coincidir exactamente con el intent-filter de la app. Un App Link HTTPS
    /// es preferible porque verifica el dominio.
    /// </summary>
    public string? AndroidAppScheme { get; set; }

    public bool HasValidResetLinkBaseUrl => TryValidateBaseUrl(out _);

    public bool IsValid()
    {
        if (!string.IsNullOrWhiteSpace(AndroidAppScheme))
        {
            var configured = AndroidAppScheme.Trim();

            if (!AppSchemeRegex().IsMatch(configured) ||
                ForbiddenSchemes.Contains(configured) ||
                string.Equals(configured, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(configured, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return string.IsNullOrWhiteSpace(ResetLinkBaseUrl) ||
               HasValidResetLinkBaseUrl;
    }

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
            !string.IsNullOrEmpty(parsed.Fragment))
        {
            return false;
        }

        var isHttps = parsed.Scheme == Uri.UriSchemeHttps;
        var isDevelopmentLoopback = parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback;
        var isConfiguredAndroidScheme = IsConfiguredAndroidScheme(parsed.Scheme);

        if (!isHttps && !isDevelopmentLoopback && !isConfiguredAndroidScheme)
        {
            return false;
        }

        if (isConfiguredAndroidScheme &&
            string.IsNullOrEmpty(parsed.Host) &&
            string.IsNullOrEmpty(parsed.AbsolutePath.Trim('/')))
        {
            return false;
        }

        baseUri = parsed;
        return true;
    }

    private bool IsConfiguredAndroidScheme(string scheme)
    {
        var configured = AndroidAppScheme?.Trim();

        return !string.IsNullOrWhiteSpace(configured) &&
               !ForbiddenSchemes.Contains(configured) &&
               !string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(scheme, configured, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9+.-]{1,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex AppSchemeRegex();
}
