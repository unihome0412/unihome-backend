using Microsoft.Extensions.Logging;
using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.Infrastructure.Identity;

/// <summary>
/// Implementación provisional mientras no exista un proveedor de correo aprobado
/// (DEC-04). No simula SMTP y no registra el código ni el correo de destino.
/// </summary>
public sealed class UnconfiguredVerificationEmailSender : IVerificationEmailSender
{
    private readonly ILogger<UnconfiguredVerificationEmailSender> _logger;

    public UnconfiguredVerificationEmailSender(ILogger<UnconfiguredVerificationEmailSender> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(
        EmailAddress email,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrEmpty(code);

        _logger.LogWarning(
            "No hay proveedor de correo configurado; el código de confirmación no fue enviado.");

        return Task.FromResult(false);
    }
}
