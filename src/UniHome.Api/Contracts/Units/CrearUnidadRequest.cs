using UniHome.Domain.Units;

namespace UniHome.Api.Contracts.Units;

public sealed record CrearUnidadRequest(
    UnitType? TipoUnidad,
    Occupancy? Ocupacion,
    int? Lugares,
    decimal? PrecioMensualPorLugarMxn,
    decimal? TamanoM2,
    ForWhom? ParaQuien,
    BathroomType? Bano,
    FurnitureType? Mobiliario,
    IReadOnlyList<string>? Muebles,
    UnitTemplate? PlantillaBase);
