using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Puerto de envío del enlace de recuperación. Application no conoce la plantilla,
/// la URL del cliente ni el proveedor; nunca registra el token ni el correo.
/// </summary>
public interface IPasswordResetEmailSender
{
    /// <summary>
    /// Devuelve true solo si el proveedor aceptó el mensaje para entrega. No
    /// implica que el buzón final lo haya recibido.
    /// </summary>
    Task<bool> SendAsync(
        EmailAddress email,
        string token,
        CancellationToken cancellationToken = default);
}
