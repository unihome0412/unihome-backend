using UniHome.Application.Publications.MyPublicationMap;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.Api.Contracts.Publications;

public sealed record MiPublicacionMapaResponse(
    Guid Id,
    double Latitud,
    double Longitud,
    decimal PrecioMensualMxn,
    UnitType TipoUnidad,
    string Etiqueta,
    PublicationState EstadoPublicacion)
{
    public static MiPublicacionMapaResponse FromView(MyPublicationMapView view) =>
        new(
            view.Id,
            view.Latitud,
            view.Longitud,
            view.PrecioMensualMxn,
            view.TipoUnidad,
            view.Etiqueta,
            view.EstadoPublicacion);
}
