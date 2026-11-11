using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Email;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Infrastructure;

public sealed class SmtpPasswordResetEmailSenderTests
{
    private const string ValidToken =
        "00112233445566778899aabbccddeeff." +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public async Task SendAsync_ConConfiguracionValida_ConstruyeEnlaceYEnvia()
    {
        var factory = new FakeSmtpClientFactory();
        var sender = CreateSender(factory, Options.Create(ValidSmtp()), ValidRecovery());

        var sent = await sender.SendAsync(
            EmailAddress.Create("ana@uni.edu"),
            ValidToken);

        Assert.True(sent);
        var client = Assert.Single(factory.Clients);
        Assert.True(client.Sent);
        Assert.Equal(PasswordResetEmailTemplate.Subject, client.LastSubject);
        Assert.Equal("ana@uni.edu", client.LastTo);
        Assert.Contains("token=", client.LastHtmlBody, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString(ValidToken), client.LastTextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_SinUrlBaseValida_NoIntentaEnviarYDevuelveFalse()
    {
        var factory = new FakeSmtpClientFactory();
        var sender = CreateSender(
            factory,
            Options.Create(ValidSmtp()),
            Options.Create(new PasswordRecoveryOptions()));

        var sent = await sender.SendAsync(
            EmailAddress.Create("ana@uni.edu"),
            ValidToken);

        Assert.False(sent);
        Assert.Empty(factory.Clients);
    }

    [Fact]
    public async Task SendAsync_ConTransporteQueFalla_DevuelveFalse()
    {
        var client = new FakeSmtpClient
        {
            OnSend = _ => throw new IOException("fallo temporal")
        };
        var sender = CreateSender(
            new ConfiguredSmtpClientFactory(client),
            Options.Create(ValidSmtp()),
            ValidRecovery());

        var sent = await sender.SendAsync(
            EmailAddress.Create("ana@uni.edu"),
            ValidToken);

        Assert.False(sent);
    }

    private static SmtpPasswordResetEmailSender CreateSender(
        ISmtpClientFactory factory,
        IOptions<SmtpOptions> smtpOptions,
        IOptions<PasswordRecoveryOptions> recoveryOptions) =>
        new(
            new SmtpEmailTransport(
                smtpOptions,
                factory,
                new RecordingLogger<SmtpEmailTransport>()),
            smtpOptions,
            recoveryOptions,
            NullLogger<SmtpPasswordResetEmailSender>.Instance);

    private static SmtpOptions ValidSmtp() =>
        new()
        {
            Host = "smtp-relay.brevo.com",
            Port = SmtpOptions.RequiredPort,
            UseStartTls = true,
            Username = "usuario-smtp@example.test",
            Password = "clave-smtp-de-prueba-no-real",
            FromEmail = "no-reply@unihome.test",
            FromName = "UniHome",
            TimeoutSeconds = SmtpOptions.DefaultTimeoutSeconds
        };

    private static IOptions<PasswordRecoveryOptions> ValidRecovery() =>
        Options.Create(new PasswordRecoveryOptions
        {
            ResetLinkBaseUrl = "https://app.unihome.test/recuperacion"
        });

    private sealed class ConfiguredSmtpClientFactory : ISmtpClientFactory
    {
        private readonly FakeSmtpClient _client;

        public ConfiguredSmtpClientFactory(FakeSmtpClient client)
        {
            _client = client;
        }

        public ISmtpClient Create() => _client;
    }
}
