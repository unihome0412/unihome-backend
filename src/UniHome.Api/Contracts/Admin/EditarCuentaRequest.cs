using System.Text.Json.Serialization;

namespace UniHome.Api.Contracts.Admin;

public sealed record EditarCuentaRequest(
    [property: JsonRequired] string NombreCompleto,
    [property: JsonRequired] string Correo);
