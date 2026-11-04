using MailKit.Net.Smtp;
using MimeKit;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Email;
using UniHome.UnitTests.TestSupport;
using SmtpAbstraction = UniHome.Infrastructure.Email.ISmtpClient;
using SmtpFactory = UniHome.Infrastructure.Email.ISmtpClientFactory;

namespace UniHome.UnitTests.Infrastructure;

public sealed class SmtpEmailTransportTests
{
    private const string Password = "clave-smtp-de-prueba-no-real";
    private const string Username = "usuario-smtp@example.test";
    private const string Destination = "ana@uni.edu";
    private const string Code = "123456";

    [Fact]
    public async Task SendAsync_ConServidorSeguro_ConectaAutenticaEnviaYDesconecta()
    {
        var factory = new FakeSmtpClientFactory();
        var logger = new RecordingLogger<SmtpEmailTransport>();
        var transport = CreateTransport(factory, logger);
        using var message = CreateMessage();

        var result = await transport.SendAsync(message);

        Assert.Equal(SmtpSendStatus.Accepted, result.Status);
        Assert.Null(result.FailureKind);

        var client = Assert.Single(factory.Clients);
        Assert.Equal("smtp-relay.brevo.com", client.Host);
        Assert.Equal(SmtpOptions.RequiredPort, client.Port);
        Assert.Equal(Username, client.Username);
        Assert.Equal(Password, client.Password);
        Assert.Equal(15_000, client.Timeout);
        Assert.True(client.Authenticated);
        Assert.True(client.Sent);
        Assert.True(client.Disconnected);
        Assert.True(client.DisconnectQuit);
        Assert.True(client.Disposed);

        Assert.Contains(
            "aceptó el mensaje",
            string.Join(" ", logger.Messages),
            StringComparison.Ordinal);

        var logs = string.Join(" ", logger.Messages);
        Assert.DoesNotContain(Password, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(Destination, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(Code, logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_SiLaConexionNoEsSegura_NoAutenticaNiEnvia()
    {
        var client = new FakeSmtpClient { Secure = false };
        var transport = CreateTransport(
            new ConfiguredSmtpClientFactory(client),
            new RecordingLogger<SmtpEmailTransport>());
        using var message = CreateMessage();

        var result = await transport.SendAsync(message);

        Assert.Equal(SmtpSendStatus.Failed, result.Status);
        Assert.Equal(SmtpFailureKind.TlsRequired, result.FailureKind);
        Assert.False(client.Authenticated);
        Assert.False(client.Sent);
    }

    [Fact]
    public async Task SendAsync_SiStartTlsEsRechazado_DevuelveTls()
    {
        var client = new FakeSmtpClient
        {
            OnConnect = _ => throw new SmtpCommandException(
                SmtpErrorCode.UnexpectedStatusCode,
                SmtpStatusCode.CommandNotImplemented,
                "STARTTLS no soportado")
        };

        var transport = CreateTransport(
            new ConfiguredSmtpClientFactory(client),
            new RecordingLogger<SmtpEmailTransport>());
        using var message = CreateMessage();

        var result = await transport.SendAsync(message);

        Assert.Equal(SmtpSendStatus.Failed, result.Status);
        Assert.Equal(SmtpFailureKind.Tls, result.FailureKind);
        Assert.False(client.Authenticated);
        Assert.False(client.Sent);
    }

    [Fact]
    public async Task SendAsync_SiAutenticacionFalla_DevuelveAuthenticationSinFiltrarSecretos()
    {
        var logger = new RecordingLogger<SmtpEmailTransport>();
        var client = new FakeSmtpClient
        {
            OnAuthenticate = _ => throw new MailKit.Security.AuthenticationException(
                "credenciales rechazadas por el servidor")
        };

        var transport = CreateTransport(
            new ConfiguredSmtpClientFactory(client),
            logger);
        using var message = CreateMessage();

        var result = await transport.SendAsync(message);

        Assert.Equal(SmtpSendStatus.Failed, result.Status);
        Assert.Equal(SmtpFailureKind.Authentication, result.FailureKind);

        var logs = string.Join(" ", logger.Messages);
        Assert.DoesNotContain(Password, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(Username, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(Destination, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(Code, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("credenciales rechazadas", logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_SiEnvioFallaTemporalmente_DevuelveTransient()
    {
        var factory = new FakeSmtpClientFactory();
        factory.Clients.Add(new FakeSmtpClient
        {
            OnSend = _ => throw new IOException("fallo temporal de red")
        });

        var transport = CreateTransport(
            new ConfiguredSmtpClientFactory(factory.Last),
            new RecordingLogger<SmtpEmailTransport>());
        using var message = CreateMessage();

        var result = await transport.SendAsync(message);

        Assert.Equal(SmtpSendStatus.Failed, result.Status);
        Assert.Equal(SmtpFailureKind.Transient, result.FailureKind);
    }

    [Fact]
    public async Task SendAsync_SiElServidorDevuelveProtocoloInvalido_DevuelveTransient()
    {
        var factory = new FakeSmtpClientFactory();
        factory.Clients.Add(new FakeSmtpClient
        {
            OnSend = _ => throw new SmtpProtocolException("respuesta inesperada")
        });

        var transport = CreateTransport(
            new ConfiguredSmtpClientFactory(factory.Last),
            new RecordingLogger<SmtpEmailTransport>());
        using var message = CreateMessage();

        var result = await transport.SendAsync(message);

        Assert.Equal(SmtpSendStatus.Failed, result.Status);
        Assert.Equal(SmtpFailureKind.Transient, result.FailureKind);
    }

    [Fact]
    public async Task SendAsync_SiElServidorRechazaElMensaje_DevuelveRejected()
    {
        var factory = new FakeSmtpClientFactory();
        factory.Clients.Add(new FakeSmtpClient
        {
            OnSend = _ => throw new SmtpCommandException(
                SmtpErrorCode.MessageNotAccepted,
                SmtpStatusCode.MailboxUnavailable,
                "buzón no disponible")
        });

        var transport = CreateTransport(
            new ConfiguredSmtpClientFactory(factory.Last),
            new RecordingLogger<SmtpEmailTransport>());
        using var message = CreateMessage();

        var result = await transport.SendAsync(message);

        Assert.Equal(SmtpSendStatus.Rejected, result.Status);
        Assert.Equal(SmtpFailureKind.Protocol, result.FailureKind);
    }

    [Fact]
    public async Task SendAsync_ConCancelacionDelLlamador_PropagaLaCancelacion()
    {
        var factory = new FakeSmtpClientFactory();
        factory.Clients.Add(new FakeSmtpClient
        {
            OnConnect = async cancellationToken =>
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
        });

        var transport = CreateTransport(
            new ConfiguredSmtpClientFactory(factory.Last),
            new RecordingLogger<SmtpEmailTransport>());
        using var message = CreateMessage();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => transport.SendAsync(message, cancellation.Token));
    }

    [Fact]
    public async Task SendAsync_CuandoSeAgotaElTimeout_DevuelveTimeout()
    {
        var options = ValidOptions();
        options.TimeoutSeconds = 1;

        var factory = new FakeSmtpClientFactory();
        factory.Clients.Add(new FakeSmtpClient
        {
            OnConnect = async cancellationToken =>
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
        });

        var transport = CreateTransport(
            new ConfiguredSmtpClientFactory(factory.Last),
            new RecordingLogger<SmtpEmailTransport>(),
            options);
        using var message = CreateMessage();

        var result = await transport.SendAsync(message);

        Assert.Equal(SmtpSendStatus.Failed, result.Status);
        Assert.Equal(SmtpFailureKind.Timeout, result.FailureKind);
    }

    [Fact]
    public async Task SendAsync_CuandoMailKitReportaTimeout_DevuelveTimeout()
    {
        var client = new FakeSmtpClient
        {
            OnSend = _ => throw new TimeoutException("tiempo de espera agotado")
        };

        var transport = CreateTransport(
            new ConfiguredSmtpClientFactory(client),
            new RecordingLogger<SmtpEmailTransport>());
        using var message = CreateMessage();

        var result = await transport.SendAsync(message);

        Assert.Equal(SmtpSendStatus.Failed, result.Status);
        Assert.Equal(SmtpFailureKind.Timeout, result.FailureKind);
    }

    [Fact]
    public async Task SendAsync_ConConfiguracionIncompleta_DevuelveConfigurationSinCrearCliente()
    {
        var options = ValidOptions();
        options.Host = null;

        var factory = new FakeSmtpClientFactory();
        var transport = CreateTransport(
            factory,
            new RecordingLogger<SmtpEmailTransport>(),
            options);
        using var message = CreateMessage();

        var result = await transport.SendAsync(message);

        Assert.Equal(SmtpSendStatus.Failed, result.Status);
        Assert.Equal(SmtpFailureKind.Configuration, result.FailureKind);
        Assert.Empty(factory.Clients);
    }

    private static SmtpEmailTransport CreateTransport(
        SmtpFactory factory,
        RecordingLogger<SmtpEmailTransport> logger,
        SmtpOptions? options = null) =>
        new(
            Microsoft.Extensions.Options.Options.Create(options ?? ValidOptions()),
            factory,
            logger);

    private static SmtpOptions ValidOptions() =>
        new()
        {
            Host = "smtp-relay.brevo.com",
            Port = SmtpOptions.RequiredPort,
            UseStartTls = true,
            Username = Username,
            Password = Password,
            FromEmail = "no-reply@unihome.test",
            FromName = "UniHome",
            TimeoutSeconds = SmtpOptions.DefaultTimeoutSeconds
        };

    private static MimeMessage CreateMessage()
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("UniHome", "no-reply@unihome.test"));
        message.To.Add(new MailboxAddress(string.Empty, Destination));
        message.Subject = $"Código {Code}";
        message.Body = new TextPart("plain") { Text = $"Código {Code}" };
        return message;
    }

    private sealed class ConfiguredSmtpClientFactory : SmtpFactory
    {
        private readonly FakeSmtpClient _client;

        public ConfiguredSmtpClientFactory(FakeSmtpClient client)
        {
            _client = client;
        }

        public SmtpAbstraction Create() => _client;
    }
}
