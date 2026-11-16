using System.Text;

namespace UniHome.Application.Units.Photos;

/// <summary>
/// Referencia opaca para entregar una foto mediante el proxy autorizado de la
/// API. Evita exponer el public_id de Cloudinary en la ruta pública.
/// </summary>
public static class PhotoReference
{
    public static string Encode(string storageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        var bytes = Encoding.UTF8.GetBytes(storageKey);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string? reference, out string storageKey)
    {
        storageKey = string.Empty;

        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 512)
        {
            return false;
        }

        var normalized = reference
            .Replace('-', '+')
            .Replace('_', '/');
        var padding = (4 - normalized.Length % 4) % 4;
        normalized = normalized.PadRight(normalized.Length + padding, '=');

        try
        {
            var bytes = Convert.FromBase64String(normalized);
            storageKey = Encoding.UTF8.GetString(bytes);
            return !string.IsNullOrWhiteSpace(storageKey);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
