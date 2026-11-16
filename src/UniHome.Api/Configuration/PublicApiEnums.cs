using UniHome.Application.Profiles.GetProfile;
using UniHome.Domain.Accounts;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.Api.Configuration;

/// <summary>
/// Enums que forman parte del contrato HTTP público. El generador nativo de
/// .NET 10 los describe como integer; el runtime usa JsonStringEnumConverter.
/// Esta lista es la fuente única para corregir el documento OpenAPI sin cambiar
/// la representación JSON real.
/// </summary>
internal static class PublicApiEnums
{
    public static IReadOnlyDictionary<string, Type> Schemas { get; } =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["Gender"] = typeof(Gender),
            ["PropertyType"] = typeof(PropertyType),
            ["PublicationState"] = typeof(PublicationState),
            ["UnitType"] = typeof(UnitType),
            ["Occupancy"] = typeof(Occupancy),
            ["ForWhom"] = typeof(ForWhom),
            ["BathroomType"] = typeof(BathroomType),
            ["FurnitureType"] = typeof(FurnitureType),
            ["UnitTemplate"] = typeof(UnitTemplate),
            ["ServiceName"] = typeof(ServiceName),
            ["ServiceMode"] = typeof(ServiceMode),
            ["AccountLabel"] = typeof(AccountLabel)
        };
}
