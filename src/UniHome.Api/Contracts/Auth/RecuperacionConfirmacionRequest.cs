namespace UniHome.Api.Contracts.Auth;

/// <summary>Contrato de EP-M01-07 (HU-006).</summary>
public sealed record RecuperacionConfirmacionRequest(string? Token, string? NuevaContrasena);
