using UniHome.Domain.Units;

namespace UniHome.Application.Units.Services;

public sealed record ServiceChargeInput(
    ServiceName Nombre,
    ServiceMode Modalidad,
    decimal? MontoMensualMxn);

public sealed record ConfigureUnitServicesCommand(
    IReadOnlyList<ServiceChargeInput>? Servicios);
