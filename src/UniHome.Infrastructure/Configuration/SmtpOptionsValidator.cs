using Microsoft.Extensions.Options;
using UniHome.Domain.Accounts;

namespace UniHome.Infrastructure.Configuration;

/// <summary>
/// Valida la configuración SMTP cuando la sección está presente. Los mensajes
/// solo nombran claves; nunca incluyen contraseñas, usuarios ni direcciones.
/// </summary>
public sealed class SmtpOptionsValidator : IValidateOptions<SmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            failures.Add($"{SmtpOptions.SectionName}:Host es obligatorio.");
        }
        else if (options.Host.Any(char.IsWhiteSpace) ||
                 options.Host.Any(char.IsControl) ||
                 options.Host.Contains("://", StringComparison.Ordinal))
        {
            failures.Add($"{SmtpOptions.SectionName}:Host debe ser un nombre de servidor, sin esquema ni espacios.");
        }

        if (options.Port != SmtpOptions.RequiredPort)
        {
            failures.Add(
                $"{SmtpOptions.SectionName}:Port debe ser {SmtpOptions.RequiredPort} para exigir STARTTLS.");
        }

        if (!options.UseStartTls)
        {
            failures.Add(
                $"{SmtpOptions.SectionName}:UseStartTls debe ser true; no se permite una conexión sin cifrar.");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            failures.Add($"{SmtpOptions.SectionName}:Username es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            failures.Add($"{SmtpOptions.SectionName}:Password es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(options.FromEmail))
        {
            failures.Add($"{SmtpOptions.SectionName}:FromEmail es obligatorio.");
        }
        else
        {
            try
            {
                _ = EmailAddress.Create(options.FromEmail);
            }
            catch (ArgumentException)
            {
                failures.Add($"{SmtpOptions.SectionName}:FromEmail no tiene un formato válido.");
            }
        }

        if (!string.IsNullOrWhiteSpace(options.FromName) &&
            (options.FromName.Trim().Length > SmtpOptions.MaxFromNameLength ||
             options.FromName.Any(char.IsControl)))
        {
            failures.Add(
                $"{SmtpOptions.SectionName}:FromName no debe superar {SmtpOptions.MaxFromNameLength} caracteres válidos.");
        }

        if (options.TimeoutSeconds is < SmtpOptions.MinTimeoutSeconds or > SmtpOptions.MaxTimeoutSeconds)
        {
            failures.Add(
                $"{SmtpOptions.SectionName}:TimeoutSeconds debe estar entre " +
                $"{SmtpOptions.MinTimeoutSeconds} y {SmtpOptions.MaxTimeoutSeconds}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
