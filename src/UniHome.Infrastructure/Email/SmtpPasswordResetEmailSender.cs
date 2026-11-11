using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Email;

/// <summary>
/// Adaptador SMTP del enlace de recuperación. Construye la URL a partir de
/// <see cref="PasswordRecoveryOptions.ResetLinkBaseUrl"/>; si la configuración no
/// es válida no simula el envío y devuelve false, sin registrar el token.
/// </summary>
public sealed class SmtpPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly SmtpEmailTransport _transport;
    private readonly IOptions<SmtpOptions> _smtpOptions;
    private readonly IOptions<PasswordRecoveryOptions> _recoveryOptions;
    private readonly ILogger<SmtpPasswordResetEmailSender> _logger;

    public SmtpPasswordResetEmailSender(
        SmtpEmailTransport transport,
        IOptions<SmtpOptions> smtpOptions,
        IOptions<PasswordRecoveryOptions> recoveryOptions,
        ILogger<SmtpPasswordResetEmailSender> logger)
    {
        _transport = transport;
        _smtpOptions = smtpOptions;
        _recoveryOptions = recoveryOptions;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        EmailAddress email,
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrEmpty(token);

        if (!_recoveryOptions.Value.TryBuildResetUrl(token, out var resetUrl))
        {
            _logger.LogError(
                "No se envió el correo de recuperación porque {Section}:ResetLinkBaseUrl no es una URL válida. " +
                "No se registró el token ni el destino.",
                PasswordRecoveryOptions.SectionName);
            return false;
        }

        using var message = PasswordResetEmailTemplate.CreateMessage(
            _smtpOptions.Value,
            email,
            resetUrl);

        var result = await _transport
            .SendAsync(message, cancellationToken)
            .ConfigureAwait(false);

        return result.Accepted;
    }
}
