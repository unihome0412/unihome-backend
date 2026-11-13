namespace UniHome.Infrastructure.Configuration;

/// <summary>
/// Configuración de Cloudinary. CloudName, ApiKey y ApiSecret deben venir de
/// User Secrets o variables de entorno; nunca de appsettings ni del repositorio.
/// La ausencia de credenciales deja el endpoint de fotos en modo fail-closed.
/// </summary>
public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public const int DefaultTimeoutSeconds = 30;
    public const int MinTimeoutSeconds = 1;
    public const int MaxTimeoutSeconds = 120;

    public string? CloudName { get; set; }

    public string? ApiKey { get; set; }

    public string? ApiSecret { get; set; }

    /// <summary>Carpeta lógica de public_ids; no es una carpeta local.</summary>
    public string Folder { get; set; } = "unihome/unidades";

    /// <summary>
    /// Base pública de la API para construir URLs absolutas del proxy de fotos.
    /// Si está vacía, se devuelve una ruta relativa que el cliente resuelve.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CloudName) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ApiSecret);

    public int EffectiveTimeoutSeconds =>
        TimeoutSeconds is >= MinTimeoutSeconds and <= MaxTimeoutSeconds
            ? TimeoutSeconds
            : DefaultTimeoutSeconds;

    public TimeSpan Timeout => TimeSpan.FromSeconds(EffectiveTimeoutSeconds);

    public string EffectiveFolder
    {
        get
        {
            var folder = string.IsNullOrWhiteSpace(Folder)
                ? "unihome/unidades"
                : Folder.Trim().Trim('/');
            return folder;
        }
    }
}
