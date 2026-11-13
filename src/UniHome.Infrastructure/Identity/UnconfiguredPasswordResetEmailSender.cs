using Microsoft.Extensions.Logging;
using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.Infrastructure.Identity;

/// <summary>
/// Implementación provisional sin proveedor SMTP. No simula el envío ni registra
/// el token o el correo de destino.
/// </summary>
public sealed class UnconfiguredPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly ILogger<UnconfiguredPasswordResetEmailSender> _logger;

    public UnconfiguredPasswordResetEmailSender(
        ILogger<UnconfiguredPasswordResetEmailSender> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(
        EmailAddress email,
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrEmpty(token);

        _logger.LogWarning(
            "No hay proveedor de correo configurado; el enlace de recuperación no fue enviado.");
        return Task.FromResult(false);
    }
}
