using Microsoft.Extensions.Options;
using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Email;

/// <summary>
/// Adaptador de correo de confirmación sobre el relé SMTP. Devuelve true cuando
/// el servidor SMTP aceptó el mensaje para entrega; no afirma que el buzón final
/// lo haya recibido.
/// </summary>
public sealed class SmtpVerificationEmailSender : IVerificationEmailSender
{
    private readonly SmtpEmailTransport _transport;
    private readonly IOptions<SmtpOptions> _options;

    public SmtpVerificationEmailSender(
        SmtpEmailTransport transport,
        IOptions<SmtpOptions> options)
    {
        _transport = transport;
        _options = options;
    }

    public async Task<bool> SendAsync(
        EmailAddress email,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrEmpty(code);

        using var message = VerificationEmailTemplate.CreateMessage(
            _options.Value,
            email,
            code);

        var result = await _transport
            .SendAsync(message, cancellationToken)
            .ConfigureAwait(false);

        return result.Accepted;
    }
}
