using UniHome.Domain.Units;

namespace UniHome.Api.Contracts.Units;

public sealed record ServicioResponse(
    ServiceName Nombre,
    ServiceMode Modalidad,
    decimal? MontoMensualMxn);
