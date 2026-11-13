using UniHome.Domain.Units;

namespace UniHome.Application.Units.CreateUnit;

public sealed record CreateUnitCommand(
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
