namespace UniHome.Api.Contracts.Auth;

/// <summary>
/// Respuesta de EP-M01-01. No incluye rol, hash de contraseña, código de
/// confirmación ni datos de sesión.
/// </summary>
public sealed record RegistroResponse(
    Guid Id,
    string Correo,
    bool CorreoConfirmado,
    bool RequiereConfirmacionCorreo,
    bool CorreoConfirmacionEnviado)
{
    public override string ToString() =>
        $"RegistroResponse {{ Id = {Id}, CorreoConfirmado = {CorreoConfirmado}, " +
        $"RequiereConfirmacionCorreo = {RequiereConfirmacionCorreo}, " +
        $"CorreoConfirmacionEnviado = {CorreoConfirmacionEnviado} }}";
}
