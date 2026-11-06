namespace UniHome.Application.Identity.Login;

/// <summary>
/// Credenciales del formulario único de acceso. No incluye rol ni tipo de cuenta.
/// </summary>
public sealed record LoginCommand(string? Email, string? Password);
