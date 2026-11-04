using System.Text.Encodings.Web;
using MimeKit;
using MimeKit.Utils;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Email;

/// <summary>
/// Plantillas del correo de confirmación. El código se escapa antes de
/// incorporarlo al HTML; no se incluyen datos personales ni enlaces externos.
/// </summary>
public static class VerificationEmailTemplate
{
    public const string Subject = "UniHome - Confirma tu correo";

    public static MimeMessage CreateMessage(
        SmtpOptions options,
        EmailAddress email,
        string code)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrEmpty(code);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromDisplayName, options.FromEmail!));
        message.To.Add(new MailboxAddress(string.Empty, email.Value));
        message.Subject = Subject;
        message.Date = DateTimeOffset.UtcNow;
        message.MessageId = MimeUtils.GenerateMessageId();
        message.Headers.Add("Auto-Submitted", "auto-generated");

        var body = new BodyBuilder
        {
            HtmlBody = RenderHtml(code),
            TextBody = RenderText(code)
        };

        message.Body = body.ToMessageBody();
        return message;
    }

    public static string RenderHtml(string code)
    {
        var safeCode = HtmlEncoder.Default.Encode(code);

        return $$"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Confirma tu correo de UniHome</title>
            </head>
            <body style="margin:0;padding:0;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#1f2933;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color:#f4f6f8;padding:24px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:520px;background-color:#ffffff;border-radius:12px;padding:32px;">
                      <tr>
                        <td style="font-size:22px;font-weight:700;color:#0f5132;padding-bottom:16px;">UniHome</td>
                      </tr>
                      <tr>
                        <td style="font-size:16px;line-height:1.5;padding-bottom:12px;">
                          Hola:
                        </td>
                      </tr>
                      <tr>
                        <td style="font-size:16px;line-height:1.5;padding-bottom:20px;">
                          Recibimos tu registro. Usa este código para confirmar tu correo electrónico:
                        </td>
                      </tr>
                      <tr>
                        <td align="center" style="padding:20px 0;">
                          <div style="display:inline-block;font-size:32px;letter-spacing:8px;font-weight:700;color:#0f5132;background-color:#e8f5ee;border-radius:8px;padding:12px 20px;">{{safeCode}}</div>
                        </td>
                      </tr>
                      <tr>
                        <td style="font-size:14px;line-height:1.5;color:#52606d;padding-bottom:12px;">
                          El código vence en 10 minutos y solo puede usarse una vez.
                        </td>
                      </tr>
                      <tr>
                        <td style="font-size:14px;line-height:1.5;color:#52606d;">
                          Si no solicitaste este registro, puedes ignorar este mensaje.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    public static string RenderText(string code) =>
        $"""
        UniHome

        Recibimos tu registro. Usa este código para confirmar tu correo electrónico:

        {code}

        El código vence en 10 minutos y solo puede usarse una vez.
        Si no solicitaste este registro, puedes ignorar este mensaje.
        """;
}
