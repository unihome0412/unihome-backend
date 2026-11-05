namespace UniHome.Api.Contracts.Auth;

/// <summary>
/// Respuesta de EP-M01-02. Idéntica para una confirmación nueva y para una
/// cuenta ya confirmada (idempotencia); no revela más datos.
/// </summary>
public sealed record ConfirmarCorreoResponse(bool CorreoConfirmado);
