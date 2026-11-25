using System.Text.Json.Serialization;

namespace UniHome.Api.Contracts.Admin;

public sealed record CambiarEstadoCuentaRequest(
    [property: JsonRequired] bool Suspendida);
