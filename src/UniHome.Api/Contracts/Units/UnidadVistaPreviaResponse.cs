using UniHome.Application.Units.Preview;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.Api.Contracts.Units;

public sealed record UnidadVistaPreviaResponse(
    Guid Id,
    string? FotoPrincipalUrl,
    decimal PrecioMensualPorLugarMxn,
    UnitType TipoUnidad,
    decimal? DistanciaKm,
    string? TiempoTraslado,
    ForWhom ParaQuien,
    bool ArrendadorVerificado,
    bool? EsFavorito,
    PublicationState EstadoPublicacion)
{
    public static UnidadVistaPreviaResponse FromView(UnitPreviewView view) =>
        new(
            view.Id,
            view.FotoPrincipalUrl,
            view.PrecioMensualPorLugarMxn,
            view.TipoUnidad,
            view.DistanciaKm,
            view.TiempoTraslado,
            view.ParaQuien,
            view.ArrendadorVerificado,
            view.EsFavorito,
            view.EstadoPublicacion);
}
