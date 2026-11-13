using UniHome.Application.Favorites;
using UniHome.Domain.Units;

namespace UniHome.Api.Contracts.Favorites;

public sealed record FavoritoTarjetaResponse(
    Guid Id,
    string? FotoPrincipalUrl,
    decimal PrecioMensualMxn,
    UnitType TipoUnidad,
    decimal? DistanciaKm,
    string? TiempoTraslado,
    ForWhom ParaQuien,
    bool ArrendadorVerificado,
    bool EsFavorito,
    string EstadoActual)
{
    public static FavoritoTarjetaResponse FromView(FavoriteCardView view) =>
        new(
            view.Id,
            view.FotoPrincipalUrl,
            view.PrecioMensualMxn,
            view.TipoUnidad,
            view.DistanciaKm,
            view.TiempoTraslado,
            view.ParaQuien,
            view.ArrendadorVerificado,
            view.EsFavorito,
            view.EstadoActual);
}

public sealed record FavoritoResponse(
    Guid UnidadId,
    FavoritoTarjetaResponse Tarjeta)
{
    public static FavoritoResponse FromView(FavoriteView view) =>
        new(view.UnidadId, FavoritoTarjetaResponse.FromView(view.Tarjeta));
}
