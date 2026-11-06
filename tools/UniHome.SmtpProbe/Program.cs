using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using MimeKit.Utils;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Email;

// Utilidad manual exclusiva de desarrollo. No se ejecuta desde la API, no
// publica rutas HTTP y no escribe credenciales ni mensajes en disco.
const string UserSecretsId = "unihome-backend-api";
const string TestSubject = "UniHome - Prueba de correo";

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly, optional: true)
    .Build();

var options = new SmtpOptions();
configuration
    .GetSection(SmtpOptions.SectionName)
    .Bind(options);

var validation = new SmtpOptionsValidator().Validate(name: null, options);

if (validation.Failed)
{
    Console.Error.WriteLine("La configuración SMTP no es válida o está incompleta:");
    foreach (var failure in validation.Failures ?? [])
    {
        Console.Error.WriteLine($"- {failure}");
    }

    Console.Error.WriteLine(
        $"Configura los valores en User Secrets del proyecto UniHome.Api ({UserSecretsId}).");
    return 2;
}

Console.Write("Correo de destino controlado por el desarrollador: ");
var destination = Console.ReadLine();

if (string.IsNullOrWhiteSpace(destination))
{
    Console.Error.WriteLine("No se indicó un correo de destino.");
    return 2;
}

EmailAddress email;

try
{
    email = EmailAddress.Create(destination);
}
catch (ArgumentException)
{
    Console.Error.WriteLine("El correo de destino no tiene un formato válido.");
    return 2;
}

Console.Write("Se enviará un mensaje de prueba al correo indicado. ¿Continuar? [s/N]: ");
var confirmation = Console.ReadLine();

if (!string.Equals(confirmation?.Trim(), "s", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("Envío cancelado.");
    return 3;
}

using var message = new MimeMessage();
message.From.Add(new MailboxAddress(options.FromDisplayName, options.FromEmail!));
message.To.Add(new MailboxAddress(string.Empty, email.Value));
message.Subject = TestSubject;
message.Date = DateTimeOffset.UtcNow;
message.MessageId = MimeUtils.GenerateMessageId();
message.Body = new TextPart("plain")
{
    Text =
        "Este es un mensaje de prueba enviado desde la utilidad local de UniHome.\n\n" +
        "Si lo recibiste, el relé SMTP aceptó el mensaje. Confirma la entrega final en el panel de Brevo."
};

var transport = new SmtpEmailTransport(
    Microsoft.Extensions.Options.Options.Create(options),
    new MailKitSmtpClientFactory(),
    NullLogger<SmtpEmailTransport>.Instance);

var result = await transport.SendAsync(message, CancellationToken.None);

switch (result.Status)
{
    case SmtpSendStatus.Accepted:
        Console.WriteLine(
            "El servidor SMTP aceptó el mensaje para su entrega. " +
            "La entrega final debe confirmarse en el panel de Brevo.");
        return 0;

    case SmtpSendStatus.Rejected:
        Console.WriteLine(
            $"El servidor SMTP rechazó el mensaje (motivo: {result.FailureKind}).");
        return 4;

    default:
        Console.WriteLine(
            $"No se pudo completar el envío (motivo: {result.FailureKind}).");
        return 5;
}
