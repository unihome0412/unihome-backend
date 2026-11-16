using UniHome.Domain.Common;
using UniHome.Domain.Properties;

namespace UniHome.Domain.Units;

/// <summary>
/// Unidad de una propiedad. Nace en Borrador; su visibilidad pública y el estado
/// derivado de ocupación se resolverán en M04/M09. La entidad valida el catálogo
/// de servicios, muebles y las reglas de ocupación de HU-031/078/081.
/// </summary>
public sealed class Unit
{
    public const int MaxPlaces = 50;
    public const int MaxFurnitureItems = 6;
    public const int MaxZoneInfoLength = 1000;
    public const int MaxRulesLength = 2000;
    public const int MaxPhotos = 10;

    private readonly List<ServiceCharge> _services;
    private readonly List<UnitPhoto> _photos;
    private readonly List<string> _furnitureItems;

    private Unit(
        Guid id,
        Guid propertyId,
        Guid ownerAccountId,
        Guid universityId,
        UnitType type,
        Occupancy occupancy,
        int places,
        decimal pricePerPlace,
        decimal sizeM2,
        ForWhom forWhom,
        BathroomType bathroom,
        FurnitureType furniture,
        IReadOnlyList<string> furnitureItems,
        UnitTemplate? template,
        string? zoneInfo,
        string? rules,
        IReadOnlyList<ServiceCharge> services,
        IReadOnlyList<UnitPhoto> photos,
        PublicationState publicationState,
        DateTimeOffset createdAt,
        UnitAvailability availability)
    {
        Id = id;
        PropertyId = propertyId;
        OwnerAccountId = ownerAccountId;
        UniversityId = universityId;
        Type = type;
        Occupancy = occupancy;
        Places = places;
        PricePerPlace = pricePerPlace;
        SizeM2 = sizeM2;
        ForWhom = forWhom;
        Bathroom = bathroom;
        Furniture = furniture;
        _furnitureItems = [.. furnitureItems];
        Template = template;
        ZoneInfo = zoneInfo;
        Rules = rules;
        _services = [.. services];
        _photos = [.. photos];
        PublicationState = publicationState;
        CreatedAt = createdAt;
        Availability = availability;
    }

    public Guid Id { get; }
    public Guid PropertyId { get; }
    public Guid OwnerAccountId { get; }
    public Guid UniversityId { get; }
    public UnitType Type { get; }
    public Occupancy Occupancy { get; }
    public int Places { get; }
    public decimal PricePerPlace { get; }
    public decimal SizeM2 { get; }
    public ForWhom ForWhom { get; }
    public BathroomType Bathroom { get; }
    public FurnitureType Furniture { get; }
    public IReadOnlyList<string> FurnitureItems => _furnitureItems.AsReadOnly();
    public UnitTemplate? Template { get; }
    public string? ZoneInfo { get; private set; }
    public string? Rules { get; private set; }
    public IReadOnlyList<ServiceCharge> Services => _services.AsReadOnly();
    public IReadOnlyList<UnitPhoto> Photos => _photos.AsReadOnly();
    public PublicationState PublicationState { get; }
    public UnitAvailability Availability { get; }
    public DateTimeOffset CreatedAt { get; }

    public static Unit Create(
        Guid propertyId,
        Guid ownerAccountId,
        Guid universityId,
        UnitType type,
        Occupancy occupancy,
        int places,
        decimal pricePerPlace,
        decimal sizeM2,
        ForWhom forWhom,
        BathroomType bathroom,
        FurnitureType furniture,
        IEnumerable<string>? furnitureItems,
        UnitTemplate? template,
        DateTimeOffset now)
    {
        if (propertyId == Guid.Empty || ownerAccountId == Guid.Empty || universityId == Guid.Empty)
        {
            throw new ArgumentException("La propiedad, el propietario y la universidad son obligatorios.");
        }

        if (!Enum.IsDefined(type) || !Enum.IsDefined(occupancy) ||
            !Enum.IsDefined(forWhom) || !Enum.IsDefined(bathroom) || !Enum.IsDefined(furniture))
        {
            throw new ArgumentException("Los valores de la unidad no son válidos.");
        }

        if (pricePerPlace <= 0 || sizeM2 <= 0)
        {
            throw new ArgumentException("El precio y el tamaño deben ser mayores que cero.");
        }

        var normalizedPlaces = occupancy == Occupancy.Individual ? 1 : places;

        if (occupancy == Occupancy.Compartida && (normalizedPlaces < 2 || normalizedPlaces > MaxPlaces))
        {
            throw new ArgumentException(
                $"Una unidad compartida debe tener entre 2 y {MaxPlaces} lugares.",
                nameof(places));
        }

        var normalizedFurniture = NormalizeFurniture(furniture, furnitureItems);

        return new Unit(
            Guid.NewGuid(),
            propertyId,
            ownerAccountId,
            universityId,
            type,
            occupancy,
            normalizedPlaces,
            pricePerPlace,
            sizeM2,
            forWhom,
            bathroom,
            furniture,
            normalizedFurniture,
            template,
            null,
            null,
            [],
            [],
            PublicationState.Borrador,
            now,
            UnitAvailability.Disponible);
    }

    public static Unit Restore(
        Guid id,
        Guid propertyId,
        Guid ownerAccountId,
        Guid universityId,
        UnitType type,
        Occupancy occupancy,
        int places,
        decimal pricePerPlace,
        decimal sizeM2,
        ForWhom forWhom,
        BathroomType bathroom,
        FurnitureType furniture,
        IEnumerable<string>? furnitureItems,
        UnitTemplate? template,
        string? zoneInfo,
        string? rules,
        IEnumerable<ServiceCharge>? services,
        IEnumerable<UnitPhoto>? photos,
        PublicationState publicationState,
        DateTimeOffset createdAt,
        UnitAvailability availability = UnitAvailability.Disponible) =>
        new(
            id,
            propertyId,
            ownerAccountId,
            universityId,
            type,
            occupancy,
            places,
            pricePerPlace,
            sizeM2,
            forWhom,
            bathroom,
            furniture,
            furnitureItems?.ToArray() ?? [],
            template,
            OptionalText(zoneInfo, MaxZoneInfoLength, "La información de zona es demasiado larga."),
            OptionalText(rules, MaxRulesLength, "Las reglas de convivencia son demasiado largas."),
            services?.ToArray() ?? [],
            photos?.ToArray() ?? [],
            publicationState,
            createdAt,
            availability);

    public void ConfigureServices(IEnumerable<ServiceCharge> services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var configured = services.ToArray();

        if (configured.Select(service => service.Name).Distinct().Count() != configured.Length)
        {
            throw new ArgumentException("No se puede repetir un servicio en la unidad.", nameof(services));
        }

        _services.Clear();
        _services.AddRange(configured);
    }

    public void ConfigureZoneRules(string? zoneInfo, string? rules)
    {
        ZoneInfo = OptionalText(zoneInfo, MaxZoneInfoLength, "La información de zona es demasiado larga.");
        Rules = OptionalText(rules, MaxRulesLength, "Las reglas de convivencia son demasiado largas.");
    }

    public void AppendPhotos(IEnumerable<UnitPhoto> photos)
    {
        ArgumentNullException.ThrowIfNull(photos);
        var pending = photos.ToArray();

        if (_photos.Count + pending.Length > MaxPhotos)
        {
            throw new ArgumentException(
                $"Una unidad puede tener como máximo {MaxPhotos} fotografías.",
                nameof(photos));
        }

        _photos.AddRange(pending);
    }

    private static List<string> NormalizeFurniture(
        FurnitureType furniture,
        IEnumerable<string>? items)
    {
        if (furniture == FurnitureType.SinMuebles)
        {
            return [];
        }

        var allowed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cama"] = "Cama",
            ["escritorio"] = "Escritorio",
            ["closet"] = "Clóset",
            ["silla"] = "Silla",
            ["sofa"] = "Sofá",
            ["refrigerador"] = "Refrigerador"
        };
        var normalized = new List<string>();

        foreach (var item in items ?? [])
        {
            var key = TextNormalization.Normalize(item);

            if (!allowed.TryGetValue(key, out var canonical) || normalized.Contains(canonical))
            {
                throw new ArgumentException("La lista de muebles contiene un valor no permitido.", nameof(items));
            }

            normalized.Add(canonical);
        }

        if (normalized.Count > MaxFurnitureItems)
        {
            throw new ArgumentException("La lista de muebles excede el máximo permitido.", nameof(items));
        }

        return normalized;
    }

    private static string? OptionalText(string? value, int maxLength, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var collapsed = TextNormalization.CollapseWhitespace(value);

        if (collapsed.Length > maxLength)
        {
            throw new ArgumentException(message, nameof(value));
        }

        return collapsed;
    }
}
