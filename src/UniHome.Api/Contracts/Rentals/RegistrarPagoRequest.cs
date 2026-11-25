using System.Text.Json.Serialization;

namespace UniHome.Api.Contracts.Rentals;

public sealed record RegistrarPagoRequest(
    [property: JsonRequired] decimal MontoMxn,
    [property: JsonRequired] DateOnly FechaRecepcion);
