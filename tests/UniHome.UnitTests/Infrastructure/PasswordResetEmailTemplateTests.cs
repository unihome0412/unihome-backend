using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Email;

namespace UniHome.UnitTests.Infrastructure;

public sealed class PasswordResetEmailTemplateTests
{
    private const string ResetUrl =
        "https://app.unihome.test/recuperacion?token=abc.def_-";

    [Fact]
    public void RenderHtml_EscapaLaUrlAntesDeInsertarla()
    {
        var html = PasswordResetEmailTemplate.RenderHtml(
            "https://app.unihome.test/<script>alert(\"x\")</script>");

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&quot;x&quot;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderHtml_IncluyeEnlaceYVigenciaDeTreintaMinutos()
    {
        var html = PasswordResetEmailTemplate.RenderHtml(ResetUrl);

        Assert.Contains(ResetUrl, html, StringComparison.Ordinal);
        Assert.Contains("30 minutos", html, StringComparison.Ordinal);
        Assert.Contains("un solo uso", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderText_IncluyeEnlaceSinHtml()
    {
        var text = PasswordResetEmailTemplate.RenderText(ResetUrl);

        Assert.Contains(ResetUrl, text, StringComparison.Ordinal);
        Assert.Contains("30 minutos", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<a ", text, StringComparison.Ordinal);
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

        using var message = PasswordResetEmailTemplate.CreateMessage(
            options,
            EmailAddress.Create("ana@uni.edu"),
            ResetUrl);

        Assert.Equal(PasswordResetEmailTemplate.Subject, message.Subject);
        Assert.Equal("no-reply@unihome.test", message.From.Mailboxes.Single().Address);
        Assert.Equal("ana@uni.edu", message.To.Mailboxes.Single().Address);
        Assert.Equal("auto-generated", message.Headers["Auto-Submitted"]);
        Assert.False(string.IsNullOrWhiteSpace(message.MessageId));
        Assert.Contains(ResetUrl, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(ResetUrl, message.HtmlBody, StringComparison.Ordinal);
    }
}
