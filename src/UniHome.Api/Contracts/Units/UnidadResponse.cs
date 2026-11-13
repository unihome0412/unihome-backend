using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.Api.Contracts.Units;

public sealed record UnidadResponse(
    Guid Id,
    Guid PropiedadId,
    Guid UniversidadId,
    UnitType TipoUnidad,
    Occupancy Ocupacion,
    int Lugares,
    decimal PrecioMensualPorLugarMxn,
    decimal TamanoM2,
    ForWhom ParaQuien,
    BathroomType Bano,
    FurnitureType Mobiliario,
    IReadOnlyList<string> Muebles,
    UnitTemplate? PlantillaBase,
    string? InformacionZona,
    string? ReglasConvivencia,
    IReadOnlyList<ServicioResponse> Servicios,
    int TotalFotos,
    PublicationState EstadoPublicacion,
    DateTimeOffset FechaAlta)
{
    public static UnidadResponse FromDomain(Unit unit) =>
        new(
            unit.Id,
            unit.PropertyId,
            unit.UniversityId,
            unit.Type,
            unit.Occupancy,
            unit.Places,
            unit.PricePerPlace,
            unit.SizeM2,
            unit.ForWhom,
            unit.Bathroom,
            unit.Furniture,
            unit.FurnitureItems,
            unit.Template,
            unit.ZoneInfo,
            unit.Rules,
            unit.Services
                .Select(service => new ServicioResponse(
                    service.Name,
                    service.Mode,
                    service.Amount))
                .ToArray(),
            unit.Photos.Count,
            unit.PublicationState,
            unit.CreatedAt);
}
