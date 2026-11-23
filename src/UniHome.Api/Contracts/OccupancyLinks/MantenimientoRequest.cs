using System.Text.Json.Serialization;

namespace UniHome.Api.Contracts.OccupancyLinks;

public sealed record MantenimientoRequest([property: JsonRequired] bool EnMantenimiento);
