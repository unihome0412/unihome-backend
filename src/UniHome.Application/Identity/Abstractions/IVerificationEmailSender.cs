using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Puerto de envío del código de confirmación. La implementación concreta decide
/// el proveedor; Application nunca registra el código ni el correo de destino.
/// </summary>
public interface IVerificationEmailSender
{
    /// <summary>
    /// Devuelve <c>true</c> solo si el proveedor aceptó el mensaje para entrega.
    /// No implica que el buzón final lo haya recibido ni permite afirmar una
    /// entrega efectiva. Sin proveedor configurado debe devolver <c>false</c>,
    /// no simular el envío.
    /// </summary>
    Task<bool> SendAsync(
        EmailAddress email,
        string code,
        CancellationToken cancellationToken = default);
}
