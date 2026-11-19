namespace UniHome.Api.Contracts.Common;

/// <summary>
/// Confirmación explícita en el cuerpo, igual que <c>EliminarCuentaRequest</c>:
/// la UI puede pedir confirmación, pero el backend la exige también.
/// </summary>
public sealed record ConfirmacionRequest(bool Confirmacion);
