namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// EP-M13/HU-096: indica si una cuenta tiene el privilegio administrativo
/// (configurado en el servidor, ARQ-004). El adaptador vive en Api porque la
/// lista configurada (<c>AdminAccessOptions</c>) es parte de la capa de
/// autorización HTTP; Application solo necesita la pregunta de negocio.
/// </summary>
public interface IAdminAccessChecker
{
    bool IsAdmin(Guid accountId);
}
