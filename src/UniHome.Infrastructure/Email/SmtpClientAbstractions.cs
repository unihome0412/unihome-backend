using MimeKit;

namespace UniHome.Infrastructure.Email;

/// <summary>
/// Resultado de entregar un mensaje al relé SMTP. "Aceptado" significa que el
/// servidor SMTP aceptó el mensaje para su entrega; no garantiza que el buzón
/// final lo haya recibido.
/// </summary>
public enum SmtpSendStatus
{
    Accepted,
    Rejected,
    Failed
}

/// <summary>Motivo seguro para diagnóstico, sin mensajes del servidor ni PII.</summary>
public enum SmtpFailureKind
{
    Configuration,
    Tls,
    TlsRequired,
    Authentication,
    Transient,
    Timeout,
    Protocol
}

public sealed record SmtpSendResult(SmtpSendStatus Status, SmtpFailureKind? FailureKind = null)
{
    public bool Accepted => Status == SmtpSendStatus.Accepted;

    public static SmtpSendResult Success { get; } = new(SmtpSendStatus.Accepted);

    public static SmtpSendResult Rejected(SmtpFailureKind failureKind) =>
        new(SmtpSendStatus.Rejected, failureKind);

    public static SmtpSendResult Failed(SmtpFailureKind failureKind) =>
        new(SmtpSendStatus.Failed, failureKind);
}

/// <summary>
/// Adaptador mínimo sobre MailKit. La conexión se expone como "StartTls" para
/// que no exista una ruta de código que pueda degradar el cifrado.
/// </summary>
public interface ISmtpClient : IDisposable
{
    bool IsSecure { get; }

    int Timeout { get; set; }

    Task ConnectStartTlsAsync(string host, int port, CancellationToken cancellationToken);

    Task AuthenticateAsync(string username, string password, CancellationToken cancellationToken);

    Task SendAsync(MimeMessage message, CancellationToken cancellationToken);

    Task DisconnectAsync(bool quit, CancellationToken cancellationToken);
}

public interface ISmtpClientFactory
{
    ISmtpClient Create();
}

public sealed class MailKitSmtpClientFactory : ISmtpClientFactory
{
    public ISmtpClient Create() => new MailKitSmtpClient();
}

internal sealed class MailKitSmtpClient : ISmtpClient
{
    private readonly MailKit.Net.Smtp.SmtpClient _client = new();

    public bool IsSecure => _client.IsSecure;

    public int Timeout
    {
        get => _client.Timeout;
        set => _client.Timeout = value;
    }

    public Task ConnectStartTlsAsync(string host, int port, CancellationToken cancellationToken) =>
        _client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls, cancellationToken);

    public Task AuthenticateAsync(string username, string password, CancellationToken cancellationToken) =>
        _client.AuthenticateAsync(username, password, cancellationToken);

    public Task SendAsync(MimeMessage message, CancellationToken cancellationToken) =>
        _client.SendAsync(message, cancellationToken);

    public Task DisconnectAsync(bool quit, CancellationToken cancellationToken) =>
        _client.DisconnectAsync(quit, cancellationToken);

    public void Dispose() => _client.Dispose();
}
