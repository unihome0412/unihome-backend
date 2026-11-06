using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Email;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Infrastructure;

public sealed class SmtpVerificationEmailSenderTests
{
    [Fact]
    public async Task SendAsync_ConTransporteAceptado_DevuelveTrueYConstruyeLaPlantilla()
    {
        var factory = new FakeSmtpClientFactory();
        var transport = CreateTransport(factory);
        var sender = new SmtpVerificationEmailSender(
            transport,
            Microsoft.Extensions.Options.Options.Create(ValidOptions()));

        var sent = await sender.SendAsync(
            EmailAddress.Create("ana@uni.edu"),
            "654321");

        Assert.True(sent);

        var client = Assert.Single(factory.Clients);
        Assert.True(client.Sent);
        Assert.Equal(VerificationEmailTemplate.Subject, client.LastSubject);
        Assert.Equal("ana@uni.edu", client.LastTo);
        Assert.Equal("no-reply@unihome.test", client.LastFrom);
        Assert.Contains("654321", client.LastHtmlBody, StringComparison.Ordinal);
        Assert.Contains("654321", client.LastTextBody, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(client.LastMessageId));
    }

    [Fact]
    public async Task SendAsync_ConTransporteQueFalla_DevuelveFalse()
    {
        var client = new FakeSmtpClient
        {
            OnSend = _ => throw new IOException("fallo temporal")
        };

        var transport = CreateTransport(new ConfiguredSmtpClientFactory(client));
        var sender = new SmtpVerificationEmailSender(
            transport,
            Microsoft.Extensions.Options.Options.Create(ValidOptions()));

        var sent = await sender.SendAsync(
            EmailAddress.Create("ana@uni.edu"),
            "654321");

        Assert.False(sent);
    }

    [Fact]
    public async Task SendAsync_EscapaElCodigoEnElHtml()
    {
        var factory = new FakeSmtpClientFactory();
        var transport = CreateTransport(factory);
        var sender = new SmtpVerificationEmailSender(
            transport,
            Microsoft.Extensions.Options.Options.Create(ValidOptions()));

        var sent = await sender.SendAsync(
            EmailAddress.Create("ana@uni.edu"),
            "<script>alert(1)</script>");

        Assert.True(sent);

        var client = Assert.Single(factory.Clients);
        Assert.DoesNotContain("<script>", client.LastHtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", client.LastHtmlBody, StringComparison.Ordinal);
    }

    private static SmtpEmailTransport CreateTransport(ISmtpClientFactory factory) =>
        new(
            Microsoft.Extensions.Options.Options.Create(ValidOptions()),
            factory,
            new RecordingLogger<SmtpEmailTransport>());

    private static SmtpOptions ValidOptions() =>
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
