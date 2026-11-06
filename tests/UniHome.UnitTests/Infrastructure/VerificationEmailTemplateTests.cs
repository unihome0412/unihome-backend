using MimeKit;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Email;

namespace UniHome.UnitTests.Infrastructure;

public sealed class VerificationEmailTemplateTests
{
    [Fact]
    public void RenderHtml_EscapaElCodigoAntesDeInsertarlo()
    {
        var html = VerificationEmailTemplate.RenderHtml("<script>alert(\"x\")</script>");

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&quot;x&quot;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderHtml_ConCodigoNormal_MuestraElCodigo()
    {
        var html = VerificationEmailTemplate.RenderHtml("123456");

        Assert.Contains(">123456<", html, StringComparison.Ordinal);
        Assert.Contains("10 minutos", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderText_IncluyeElCodigoYSinHtml()
    {
        var text = VerificationEmailTemplate.RenderText("123456");

        Assert.Contains("123456", text, StringComparison.Ordinal);
        Assert.Contains("10 minutos", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateMessage_ConstruyeRemitenteDestinatarioYAsunto()
    {
        var options = new SmtpOptions
        {
            Host = "smtp-relay.brevo.com",
            Username = "usuario-smtp@example.test",
            Password = "clave-smtp-de-prueba-no-real",
            FromEmail = "no-reply@unihome.test",
            FromName = "UniHome"
        };

        using var message = VerificationEmailTemplate.CreateMessage(
            options,
            EmailAddress.Create("ana@uni.edu"),
            "123456");

        Assert.Equal(VerificationEmailTemplate.Subject, message.Subject);
        Assert.Equal("no-reply@unihome.test", message.From.Mailboxes.Single().Address);
        Assert.Equal("ana@uni.edu", message.To.Mailboxes.Single().Address);
        Assert.Equal("auto-generated", message.Headers["Auto-Submitted"]);
        Assert.False(string.IsNullOrWhiteSpace(message.MessageId));
        Assert.Contains("123456", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("123456", message.HtmlBody, StringComparison.Ordinal);
    }
}
