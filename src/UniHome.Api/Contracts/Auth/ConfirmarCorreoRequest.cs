namespace UniHome.Api.Contracts.Auth;

/// <summary>
/// Contrato de EP-M01-02. Decisión aprobada: recibe correo y código, sin token
/// de sesión de registro (ese contrato no está definido en la spec).
/// </summary>
public sealed record ConfirmarCorreoRequest(string? Correo, string? Codigo);
