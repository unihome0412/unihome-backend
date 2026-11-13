using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.Application.Units.Preview;

/// <summary>
/// Tarjeta de vista previa construida con datos persistidos. No incluye datos
/// privados y no declara publicada una unidad en Borrador.
/// </summary>
public sealed record UnitPreviewView(
    Guid Id,
    string? FotoPrincipalUrl,
    decimal PrecioMensualPorLugarMxn,
    UnitType TipoUnidad,
    decimal? DistanciaKm,
    string? TiempoTraslado,
    ForWhom ParaQuien,
    bool ArrendadorVerificado,
    bool? EsFavorito,
    PublicationState EstadoPublicacion);
