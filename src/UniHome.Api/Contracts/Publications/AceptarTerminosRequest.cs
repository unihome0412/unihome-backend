namespace UniHome.Api.Contracts.Publications;

public sealed record AceptarTerminosRequest(string? VersionTerminos);

public sealed record AceptarTerminosResponse(
    bool Aceptada,
    string VersionTerminos,
    DateTimeOffset FechaAceptacion);
