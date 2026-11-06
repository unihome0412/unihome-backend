namespace UniHome.Api.Contracts.Auth;

public sealed record LoginRequest(string? Correo, string? Contrasena);
