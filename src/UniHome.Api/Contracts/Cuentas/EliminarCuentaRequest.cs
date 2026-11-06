namespace UniHome.Api.Contracts.Cuentas;

/// <summary>
/// Contrato de EP-M01-09 (HU-096). <see cref="ConfirmacionPerdidaInformacion"/>
/// debe ser explícitamente <c>true</c>; su ausencia u omisión se trata igual que
/// <c>false</c>.
/// </summary>
public sealed record EliminarCuentaRequest(
    string? Contrasena,
    bool ConfirmacionPerdidaInformacion);
