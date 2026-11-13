using UniHome.Domain.Common;

namespace UniHome.Domain.Properties;

/// <summary>
/// Propiedad del flujo M05. Nace en Borrador; el envío a revisión y la decisión
/// administrativa pertenecen al Sprint 4 y no se exponen aquí.
/// </summary>
public sealed class Property
{
    public const int MaxNameLength = 120;
    public const int MaxAddressLength = 300;
    public const int MaxColoniaLength = 150;
    public const int MaxFloorLength = 20;
    public const int MaxUnitNumberLength = 30;

    private Property(
        Guid id,
        Guid ownerAccountId,
        string name,
        string nameNormalized,
        string address,
        string addressNormalized,
        string colonia,
        string coloniaNormalized,
        PropertyType propertyType,
        Guid universityId,
        double? latitude,
        double? longitude,
        string? floor,
        string? unitNumber,
        PublicationState publicationState,
        DateTimeOffset createdAt)
    {
        Id = id;
        OwnerAccountId = ownerAccountId;
        Name = name;
        NameNormalized = nameNormalized;
        Address = address;
        AddressNormalized = addressNormalized;
        Colonia = colonia;
        ColoniaNormalized = coloniaNormalized;
        PropertyType = propertyType;
        UniversityId = universityId;
        Latitude = latitude;
        Longitude = longitude;
        Floor = floor;
        UnitNumber = unitNumber;
        PublicationState = publicationState;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid OwnerAccountId { get; }
    public string Name { get; }
    public string NameNormalized { get; }
    public string Address { get; }
    public string AddressNormalized { get; }
    public string Colonia { get; }
    public string ColoniaNormalized { get; }
    public PropertyType PropertyType { get; }
    public Guid UniversityId { get; }
    public double? Latitude { get; }
    public double? Longitude { get; }
    public string? Floor { get; }
    public string? UnitNumber { get; }
    public PublicationState PublicationState { get; }
    public DateTimeOffset CreatedAt { get; }

    public static Property Create(
        Guid ownerAccountId,
        string? name,
        string? address,
        string? colonia,
        PropertyType propertyType,
        Guid universityId,
        double? latitude,
        double? longitude,
        string? floor,
        string? unitNumber,
        DateTimeOffset now)
    {
        if (ownerAccountId == Guid.Empty)
        {
            throw new ArgumentException("El propietario es obligatorio.", nameof(ownerAccountId));
        }

        if (universityId == Guid.Empty)
        {
            throw new ArgumentException("La universidad es obligatoria.", nameof(universityId));
        }

        if (!Enum.IsDefined(propertyType))
        {
            throw new ArgumentException("El tipo de propiedad no es válido.", nameof(propertyType));
        }

        var normalizedName = RequireText(name, MaxNameLength, "El nombre es obligatorio.");
        var normalizedAddress = RequireText(address, MaxAddressLength, "La dirección es obligatoria.");
        var normalizedColonia = RequireText(colonia, MaxColoniaLength, "La colonia es obligatoria.");
        var normalizedFloor = OptionalText(floor, MaxFloorLength, "El piso no debe superar la longitud permitida.");
        var normalizedUnitNumber = OptionalText(
            unitNumber,
            MaxUnitNumberLength,
            "El número de unidad no debe superar la longitud permitida.");

        if ((latitude is null) != (longitude is null))
        {
            throw new ArgumentException(
                "La latitud y la longitud deben enviarse juntas.",
                nameof(latitude));
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new ArgumentException("Las coordenadas no están en un rango válido.", nameof(latitude));
        }

        if (propertyType == PropertyType.Edificio &&
            (string.IsNullOrWhiteSpace(normalizedFloor) ||
             string.IsNullOrWhiteSpace(normalizedUnitNumber)))
        {
            throw new ArgumentException(
                "El piso y el número de unidad son obligatorios para un edificio.",
                nameof(floor));
        }

        if (propertyType != PropertyType.Edificio)
        {
            normalizedFloor = null;
            normalizedUnitNumber = null;
        }

        return new Property(
            Guid.NewGuid(),
            ownerAccountId,
            TextNormalization.CollapseWhitespace(name!),
            TextNormalization.Normalize(name!),
            TextNormalization.CollapseWhitespace(address!),
            TextNormalization.Normalize(address!),
            TextNormalization.CollapseWhitespace(colonia!),
            TextNormalization.Normalize(colonia!),
            propertyType,
            universityId,
            latitude,
            longitude,
            normalizedFloor,
            normalizedUnitNumber,
            PublicationState.Borrador,
            now);
    }

    public static Property Restore(
        Guid id,
        Guid ownerAccountId,
        string name,
        string address,
        string colonia,
        PropertyType propertyType,
        Guid universityId,
        double? latitude,
        double? longitude,
        string? floor,
        string? unitNumber,
        PublicationState publicationState,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador es obligatorio.", nameof(id));
        }

        return new Property(
            id,
            ownerAccountId,
            TextNormalization.CollapseWhitespace(name),
            TextNormalization.Normalize(name),
            TextNormalization.CollapseWhitespace(address),
            TextNormalization.Normalize(address),
            TextNormalization.CollapseWhitespace(colonia),
            TextNormalization.Normalize(colonia),
            propertyType,
            universityId,
            latitude,
            longitude,
            floor,
            unitNumber,
            publicationState,
            createdAt);
    }

    private static string RequireText(string? value, int maxLength, string missingMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(missingMessage, nameof(value));
        }

        var collapsed = TextNormalization.CollapseWhitespace(value);

        if (collapsed.Length > maxLength)
        {
            throw new ArgumentException(
                $"El valor no debe superar {maxLength} caracteres.",
                nameof(value));
        }

        return collapsed;
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
