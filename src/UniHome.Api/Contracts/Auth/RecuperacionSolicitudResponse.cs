namespace UniHome.Api.Contracts.Auth;

/// <summary>
/// Respuesta genérica de EP-M01-06. Es idéntica exista o no la cuenta y no
/// incluye correo, token ni estado del envío.
/// </summary>
public sealed record RecuperacionSolicitudResponse(string Mensaje)
{
    public const string MensajeGenerico =
        "Si el correo está registrado, recibirás instrucciones para restablecer tu contraseña";
}
