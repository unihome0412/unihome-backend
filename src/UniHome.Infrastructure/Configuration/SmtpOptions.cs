namespace UniHome.Infrastructure.Configuration;

/// <summary>
/// Configuración del relé SMTP. Los valores deben obtenerse de User Secrets o
/// variables de entorno; no se incluyen en appsettings ni en el repositorio.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Puerto exigido para STARTTLS; no se permite degradar a texto plano.</summary>
    public const int RequiredPort = 587;

    public const int DefaultTimeoutSeconds = 15;
    public const int MinTimeoutSeconds = 1;
    public const int MaxTimeoutSeconds = 120;
    public const int MaxFromNameLength = 200;

    public string? Host { get; set; }

    public int Port { get; set; } = RequiredPort;

    public bool UseStartTls { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? FromEmail { get; set; }

    public string? FromName { get; set; }

    /// <summary>
    /// Tiempo máximo por operación SMTP. Es opcional en configuración y existe
    /// para poder acotar la espera sin depender del valor predeterminado de MailKit.
    /// </summary>
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    public TimeSpan Timeout => TimeSpan.FromSeconds(
        TimeoutSeconds is >= MinTimeoutSeconds and <= MaxTimeoutSeconds
            ? TimeoutSeconds
            : DefaultTimeoutSeconds);

    public int TimeoutMilliseconds => checked((int)Timeout.TotalMilliseconds);

    public string FromDisplayName =>
        string.IsNullOrWhiteSpace(FromName) ? "UniHome" : FromName.Trim();
}
