using UniHome.Domain.Units;

namespace UniHome.Application.Favorites;

public sealed record FavoriteCardView(
    Guid Id,
    string? FotoPrincipalUrl,
    decimal PrecioMensualMxn,
    UnitType TipoUnidad,
    decimal? DistanciaKm,
    string? TiempoTraslado,
    ForWhom ParaQuien,
    bool ArrendadorVerificado,
    bool EsFavorito,
    string EstadoActual);

public sealed record FavoriteView(Guid UnidadId, FavoriteCardView Tarjeta);
