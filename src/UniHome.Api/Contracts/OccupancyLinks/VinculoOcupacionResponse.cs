using UniHome.Application.OccupancyLinks.JoinWithCode;
using UniHome.Domain.OccupancyLinks;

namespace UniHome.Api.Contracts.OccupancyLinks;

public sealed record VinculoOcupacionResponse(
    Guid UnidadId,
    OccupancyStatus Estado,
    DateTimeOffset FechaVinculo)
{
    public static VinculoOcupacionResponse FromView(OccupancyLinkView view) =>
        new(view.UnidadId, view.Estado, view.FechaVinculo);
}
