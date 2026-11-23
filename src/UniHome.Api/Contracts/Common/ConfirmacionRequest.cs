using System.Text.Json.Serialization;

namespace UniHome.Api.Contracts.Common;

/// <summary>
/// Confirmación explícita en el cuerpo, igual que <c>EliminarCuentaRequest</c>:
/// la UI puede pedir confirmación, pero el backend la exige también.
/// <c>JsonRequired</c> hace que omitir el campo responda 400 en vez de
/// interpretarse como <c>false</c>.
/// </summary>
public sealed record ConfirmacionRequest([property: JsonRequired] bool Confirmacion);
