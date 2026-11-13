using UniHome.Domain.Units;

namespace UniHome.Api.Contracts.Units;

public sealed record ServicioInput(
    ServiceName Nombre,
    ServiceMode Modalidad,
    decimal? MontoMensualMxn);

public sealed record ServiciosRequest(IReadOnlyList<ServicioInput>? Servicios);
