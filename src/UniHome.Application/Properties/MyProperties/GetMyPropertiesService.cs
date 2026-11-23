using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.Application.Properties.MyProperties;

/// <summary>
/// EP-M05-11 (HU-033): propiedades del titular con el total de habitaciones,
/// cuántas están disponibles y el estado de cada una. Permite filtrar por tipo
/// de propiedad y por el estado de al menos una habitación. No crea colecciones
/// ni índices nuevos; reutiliza M05-001.
/// </summary>
public sealed class GetMyPropertiesService
{
    private readonly IPropertyRepository _properties;
    private readonly IUnitRepository _units;

    public GetMyPropertiesService(IPropertyRepository properties, IUnitRepository units)
    {
        _properties = properties;
        _units = units;
    }

    public async Task<IReadOnlyList<MyPropertyView>> ListAsync(
        Guid ownerAccountId,
        PropertyType? tipoPropiedad,
        UnitAvailability? estadoHabitacion,
        CancellationToken cancellationToken = default)
    {
        var properties = await _properties
            .ListByOwnerAsync(ownerAccountId, cancellationToken)
            .ConfigureAwait(false);
        var units = await _units
            .ListByOwnerAsync(ownerAccountId, cancellationToken)
            .ConfigureAwait(false);

        var unitsByProperty = units
            .GroupBy(unit => unit.PropertyId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Unit>)group
                    .OrderBy(unit => unit.CreatedAt)
                    .ThenBy(unit => unit.Id)
                    .ToArray());

        var views = new List<MyPropertyView>();

        foreach (var property in properties)
        {
            if (tipoPropiedad is not null && property.PropertyType != tipoPropiedad.Value)
            {
                continue;
            }

            if (!unitsByProperty.TryGetValue(property.Id, out var propertyUnits))
            {
                propertyUnits = [];
            }

            if (estadoHabitacion is not null &&
                !propertyUnits.Any(unit => unit.Availability == estadoHabitacion.Value))
            {
                continue;
            }

            var habitaciones = propertyUnits
                .Select(unit => new MyPropertyRoomView(unit.Id, unit.Type, unit.Availability))
                .ToArray();

            views.Add(new MyPropertyView(
                property.Id,
                property.Name,
                property.PropertyType,
                TotalHabitaciones: habitaciones.Length,
                HabitacionesDisponibles: habitaciones.Count(
                    room => room.Estado == UnitAvailability.Disponible),
                habitaciones));
        }

        return views;
    }
}

public sealed record MyPropertyView(
    Guid Id,
    string Nombre,
    PropertyType TipoPropiedad,
    int TotalHabitaciones,
    int HabitacionesDisponibles,
    IReadOnlyList<MyPropertyRoomView> Habitaciones);

public sealed record MyPropertyRoomView(
    Guid Id,
    UnitType TipoUnidad,
    UnitAvailability Estado);
