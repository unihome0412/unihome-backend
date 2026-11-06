using System.Net.Sockets;
using System.Security.Authentication;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Email;

/// <summary>
/// Envía un mensaje por SMTP con STARTTLS obligatorio. No registra credenciales,
/// destinatarios, códigos ni el cuerpo del mensaje.
/// </summary>
public sealed class SmtpEmailTransport
{
    private readonly IOptions<SmtpOptions> _options;
    private readonly ISmtpClientFactory _clientFactory;
    private readonly ILogger<SmtpEmailTransport> _logger;

    public SmtpEmailTransport(
        IOptions<SmtpOptions> options,
        ISmtpClientFactory clientFactory,
        ILogger<SmtpEmailTransport> logger)
    {
        _options = options;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task<SmtpSendResult> SendAsync(
        MimeMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var options = _options.Value;

        if (string.IsNullOrWhiteSpace(options.Host) ||
            string.IsNullOrWhiteSpace(options.Username) ||
            string.IsNullOrWhiteSpace(options.Password) ||
            string.IsNullOrWhiteSpace(options.FromEmail))
        {
            _logger.LogWarning("La configuración SMTP está incompleta; no se envió el mensaje.");
            return SmtpSendResult.Failed(SmtpFailureKind.Configuration);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.Timeout);

        var token = timeoutCts.Token;
        var stage = SmtpStage.Connect;

        using var client = _clientFactory.Create();
        client.Timeout = options.TimeoutMilliseconds;

        var connected = false;

        try
        {
            await client
                .ConnectStartTlsAsync(options.Host, options.Port, token)
                .ConfigureAwait(false);
            connected = true;

            if (!client.IsSecure)
            {
                _logger.LogError(
                    "El servidor SMTP no estableció una conexión cifrada; no se envió el mensaje.");
                return SmtpSendResult.Failed(SmtpFailureKind.TlsRequired);
            }

            stage = SmtpStage.Authenticate;
            await client
                .AuthenticateAsync(options.Username, options.Password, token)
                .ConfigureAwait(false);

            stage = SmtpStage.Send;
            await client.SendAsync(message, token).ConfigureAwait(false);

            _logger.LogInformation(
                "El servidor SMTP aceptó el mensaje para su entrega.");
            return SmtpSendResult.Success;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Se agotó el tiempo de espera de la operación SMTP.");
            return SmtpSendResult.Failed(SmtpFailureKind.Timeout);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            _logger.LogWarning(
                "Se agotó el tiempo de espera de la operación SMTP ({ExceptionType}).",
                exception.GetType().Name);
            return SmtpSendResult.Failed(SmtpFailureKind.Timeout);
        }
        catch (MailKit.Security.AuthenticationException exception)
        {
            _logger.LogWarning(
                "El servidor SMTP rechazó la autenticación ({ExceptionType}).",
                exception.GetType().Name);
            return SmtpSendResult.Failed(SmtpFailureKind.Authentication);
        }
        catch (SmtpCommandException exception)
        {
            var failureKind = stage switch
            {
                SmtpStage.Authenticate => SmtpFailureKind.Authentication,
                SmtpStage.Send => SmtpFailureKind.Protocol,
                _ => SmtpFailureKind.Tls
            };

            _logger.LogWarning(
                "El servidor SMTP rechazó la operación {Stage} con estado {StatusCode}.",
                stage,
                exception.StatusCode);

            return stage == SmtpStage.Send
                ? SmtpSendResult.Rejected(failureKind)
                : SmtpSendResult.Failed(failureKind);
        }
        catch (SmtpProtocolException exception)
        {
            _logger.LogWarning(
                "El servidor SMTP devolvió una respuesta no válida ({ExceptionType}).",
                exception.GetType().Name);
            return SmtpSendResult.Failed(SmtpFailureKind.Transient);
        }
        catch (AuthenticationException exception)
        {
            _logger.LogWarning(
                "No se pudo establecer una conexión TLS con el servidor SMTP ({ExceptionType}).",
                exception.GetType().Name);

            return SmtpSendResult.Failed(
                stage == SmtpStage.Authenticate
                    ? SmtpFailureKind.Authentication
                    : SmtpFailureKind.Tls);
        }
        catch (Exception exception) when (exception is IOException or SocketException)
        {
            _logger.LogWarning(
                "Error temporal de red al comunicarse con el servidor SMTP ({ExceptionType}).",
                exception.GetType().Name);
            return SmtpSendResult.Failed(SmtpFailureKind.Transient);
        }
        catch (Exception exception)
        {
            // No se registra el mensaje de la excepción: puede contener datos del
            // servidor o del destinatario. El tipo es suficiente para diagnóstico.
            _logger.LogError(
                "Falló la operación SMTP en la etapa {Stage} ({ExceptionType}).",
                stage,
                exception.GetType().Name);

            return SmtpSendResult.Failed(
                stage == SmtpStage.Connect
                    ? SmtpFailureKind.Tls
                    : SmtpFailureKind.Protocol);
        }
        finally
        {
            if (connected)
            {
                try
                {
                    await client
                        .DisconnectAsync(quit: true, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.LogDebug(
                        "No se pudo cerrar la conexión SMTP ({ExceptionType}).",
                        exception.GetType().Name);
                }
            }
        }
    }

    private enum SmtpStage
    {
        Connect,
        Authenticate,
        Send
    }
}
