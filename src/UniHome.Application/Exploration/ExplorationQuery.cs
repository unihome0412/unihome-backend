using UniHome.Domain.Units;

namespace UniHome.Application.Exploration;

public sealed record ExplorationQuery(
    Guid UniversidadId,
    decimal? DistanciaMaxKm,
    decimal? PrecioMaxMxn,
    bool? SoloIndividual,
    ForWhom? ParaQuien,
    int Page = 1,
    int PageSize = 20);

public sealed record ExplorationPage<T>(IReadOnlyList<T> Items, long Total);
