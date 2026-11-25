using System.Text.Json.Serialization;
using UniHome.Domain.Reparaciones;

namespace UniHome.Api.Contracts.Reparaciones;

public sealed record CambiarEstadoReparacionRequest(
    [property: JsonRequired] ReparacionEstado Estado);
