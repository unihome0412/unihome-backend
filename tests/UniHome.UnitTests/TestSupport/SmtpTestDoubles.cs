using Microsoft.Extensions.Logging;
using MimeKit;
using UniHome.Infrastructure.Email;

namespace UniHome.UnitTests.TestSupport;

internal sealed class FakeSmtpClient : ISmtpClient
{
    public bool Secure { get; set; } = true;

    public bool IsSecure => Secure;

    public int Timeout { get; set; }

    public bool CheckCertificateRevocation { get; set; } = true;

    public string? Host { get; private set; }

    public int Port { get; private set; }

    public string? Username { get; private set; }

    public string? Password { get; private set; }

    public bool Connected { get; private set; }

    public bool Authenticated { get; private set; }

    public bool Sent { get; private set; }

    public bool Disconnected { get; private set; }

    public bool DisconnectQuit { get; private set; }

    public bool Disposed { get; private set; }

    public string? LastSubject { get; private set; }

    public string? LastHtmlBody { get; private set; }

    public string? LastTextBody { get; private set; }

    public string? LastTo { get; private set; }

    public string? LastFrom { get; private set; }

    public string? LastMessageId { get; private set; }

    public Func<CancellationToken, Task>? OnConnect { get; set; }

    public Func<CancellationToken, Task>? OnAuthenticate { get; set; }

    public Func<CancellationToken, Task>? OnSend { get; set; }

    public async Task ConnectStartTlsAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        Host = host;
        Port = port;
        Connected = true;

        if (OnConnect is not null)
        {
            await OnConnect(cancellationToken);
        }
    }

    public async Task AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        Username = username;
        Password = password;

        if (OnAuthenticate is not null)
        {
            await OnAuthenticate(cancellationToken);
        }

        Authenticated = true;
    }

    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        LastSubject = message.Subject;
        LastHtmlBody = message.HtmlBody;
        LastTextBody = message.TextBody;
        LastTo = message.To.Mailboxes.FirstOrDefault()?.Address;
        LastFrom = message.From.Mailboxes.FirstOrDefault()?.Address;
        LastMessageId = message.MessageId;

        if (OnSend is not null)
        {
            await OnSend(cancellationToken);
        }

        Sent = true;
    }

    public Task DisconnectAsync(bool quit, CancellationToken cancellationToken)
    {
        Disconnected = true;
        DisconnectQuit = quit;
        return Task.CompletedTask;
    }

    public void Dispose() => Disposed = true;
}

internal sealed class FakeSmtpClientFactory : ISmtpClientFactory
{
    public List<FakeSmtpClient> Clients { get; } = [];

    public FakeSmtpClient Last => Clients[^1];

    public ISmtpClient Create()
    {
        var client = new FakeSmtpClient();
        Clients.Add(client);
        return client;
    }
}

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));

        if (exception is not null)
        {
            Messages.Add($"exception={exception.GetType().FullName}");
        }
    }
}
