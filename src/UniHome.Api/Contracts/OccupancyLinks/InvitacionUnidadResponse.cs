using UniHome.Application.OccupancyLinks.GetInvitation;

namespace UniHome.Api.Contracts.OccupancyLinks;

public sealed record InvitacionUnidadResponse(string Codigo, DateTimeOffset VigenciaHasta)
{
    public static InvitacionUnidadResponse FromView(UnitInvitationView view) =>
        new(view.Codigo, view.VigenciaHasta);
}
