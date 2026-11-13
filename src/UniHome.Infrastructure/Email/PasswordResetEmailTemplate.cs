using System.Text.Encodings.Web;
using MimeKit;
using MimeKit.Utils;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Email;

/// <summary>
/// Plantilla del correo de recuperación. El enlace y el token se escapan antes de
/// incorporarse al HTML; no se incluyen datos personales ni enlaces externos
/// distintos del configurado para el cliente.
/// </summary>
public static class PasswordResetEmailTemplate
{
    public const string Subject = "UniHome - Restablece tu contraseña";
    public const string LifetimeText = "30 minutos";

    public static MimeMessage CreateMessage(
        SmtpOptions options,
        EmailAddress email,
        string resetUrl)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrEmpty(resetUrl);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromDisplayName, options.FromEmail!));
        message.To.Add(new MailboxAddress(string.Empty, email.Value));
        message.Subject = Subject;
        message.Date = DateTimeOffset.UtcNow;
        message.MessageId = MimeUtils.GenerateMessageId();
        message.Headers.Add("Auto-Submitted", "auto-generated");

        var body = new BodyBuilder
        {
            HtmlBody = RenderHtml(resetUrl),
            TextBody = RenderText(resetUrl)
        };

        message.Body = body.ToMessageBody();
        return message;
    }

    public static string RenderHtml(string resetUrl)
    {
        var safeUrl = HtmlEncoder.Default.Encode(resetUrl);

        return $$"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Restablece tu contraseña de UniHome</title>
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
                          Recibimos una solicitud para restablecer tu contraseña. Usa este enlace de un solo uso:
                        </td>
                      </tr>
                      <tr>
                        <td align="center" style="padding:20px 0;">
                          <a href="{{safeUrl}}" style="display:inline-block;font-size:16px;font-weight:700;color:#ffffff;background-color:#0f5132;border-radius:8px;padding:12px 20px;text-decoration:none;">Crear nueva contraseña</a>
                        </td>
                      </tr>
                      <tr>
                        <td style="font-size:14px;line-height:1.5;color:#52606d;padding-bottom:12px;">
                          El enlace vence en {{LifetimeText}} y solo puede usarse una vez.
                        </td>
                      </tr>
                      <tr>
                        <td style="font-size:14px;line-height:1.5;color:#52606d;">
                          Si no solicitaste este cambio, puedes ignorar este mensaje.
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

    public static string RenderText(string resetUrl) =>
        $"""
        UniHome

        Recibimos una solicitud para restablecer tu contraseña. Usa este enlace de un solo uso:

        {resetUrl}

        El enlace vence en {LifetimeText} y solo puede usarse una vez.
        Si no solicitaste este cambio, puedes ignorar este mensaje.
        """;
}
