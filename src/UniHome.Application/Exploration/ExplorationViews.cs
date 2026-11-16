using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.Application.Exploration;

public sealed record UnitMapView(
    Guid Id,
    double Latitud,
    double Longitud,
    decimal PrecioMensualMxn);

public sealed record UnitCardView(
    Guid Id,
    string? FotoPrincipalUrl,
    decimal PrecioMensualMxn,
    UnitType TipoUnidad,
    decimal? DistanciaKm,
    string? TiempoTraslado,
    ForWhom ParaQuien,
    bool ArrendadorVerificado,
    bool? EsFavorito);

public sealed record UnitServiceView(string Nombre, decimal MontoMensualMxn);

public sealed record UnitDetailView(
    Guid Id,
    string? FotoPrincipalUrl,
    IReadOnlyList<string> GaleriaFotos,
    decimal PrecioMensualMxn,
    UnitType TipoUnidad,
    decimal? DistanciaKm,
    string? TiempoTraslado,
    ForWhom ParaQuien,
    bool ArrendadorVerificado,
    bool? EsFavorito,
    Occupancy Ocupacion,
    int Lugares,
    decimal TamanoM2,
    BathroomType Bano,
    FurnitureType Mobiliario,
    IReadOnlyList<string> ServiciosIncluidos,
    IReadOnlyList<UnitServiceView> ServiciosAparte,
    PropertyType TipoPropiedad);
