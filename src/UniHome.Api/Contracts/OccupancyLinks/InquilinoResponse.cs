using UniHome.Application.OccupancyLinks.ListTenants;

namespace UniHome.Api.Contracts.OccupancyLinks;

public sealed record InquilinoResponse(
    Guid AccountId,
    string NombreCompleto,
    DateTimeOffset FechaVinculo)
{
    public static InquilinoResponse FromView(UnitTenantView view) =>
        new(view.AccountId, view.NombreCompleto, view.FechaVinculo);
}
