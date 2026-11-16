using UniHome.Application.Exploration;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.Api.Contracts.Exploration;

public sealed record UnidadMapaResponse(
    Guid Id,
    double Latitud,
    double Longitud,
    decimal PrecioMensualMxn)
{
    public static UnidadMapaResponse FromView(UnitMapView view) =>
        new(view.Id, view.Latitud, view.Longitud, view.PrecioMensualMxn);
}

public sealed record UnidadTarjetaResponse(
    Guid Id,
    string? FotoPrincipalUrl,
    decimal PrecioMensualMxn,
    UnitType TipoUnidad,
    decimal? DistanciaKm,
    string? TiempoTraslado,
    ForWhom ParaQuien,
    bool ArrendadorVerificado,
    bool? EsFavorito)
{
    public static UnidadTarjetaResponse FromView(UnitCardView view) =>
        new(
            view.Id,
            view.FotoPrincipalUrl,
            view.PrecioMensualMxn,
            view.TipoUnidad,
            view.DistanciaKm,
            view.TiempoTraslado,
            view.ParaQuien,
            view.ArrendadorVerificado,
            view.EsFavorito);
}

public sealed record ServicioAparteResponse(string Nombre, decimal MontoMensualMxn);

public sealed record UnidadFichaResponse(
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
    IReadOnlyList<ServicioAparteResponse> ServiciosAparte,
    PropertyType TipoPropiedad)
{
    public static UnidadFichaResponse FromView(UnitDetailView view) =>
        new(
            view.Id,
            view.FotoPrincipalUrl,
            view.GaleriaFotos,
            view.PrecioMensualMxn,
            view.TipoUnidad,
            view.DistanciaKm,
            view.TiempoTraslado,
            view.ParaQuien,
            view.ArrendadorVerificado,
            view.EsFavorito,
            view.Ocupacion,
            view.Lugares,
            view.TamanoM2,
            view.Bano,
            view.Mobiliario,
            view.ServiciosIncluidos,
            view.ServiciosAparte
                .Select(service => new ServicioAparteResponse(
                    service.Nombre,
                    service.MontoMensualMxn))
                .ToArray(),
            view.TipoPropiedad);
}
