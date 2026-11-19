using UniHome.Application.Properties.MyProperties;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.Api.Contracts.Properties;

public sealed record MiHabitacionResponse(
    Guid Id,
    UnitType TipoUnidad,
    UnitAvailability Estado)
{
    public static MiHabitacionResponse FromView(MyPropertyRoomView view) =>
        new(view.Id, view.TipoUnidad, view.Estado);
}

public sealed record MiPropiedadResponse(
    Guid Id,
    string Nombre,
    PropertyType TipoPropiedad,
    int TotalHabitaciones,
    int HabitacionesDisponibles,
    IReadOnlyList<MiHabitacionResponse> Habitaciones)
{
    public static MiPropiedadResponse FromView(MyPropertyView view) =>
        new(
            view.Id,
            view.Nombre,
            view.TipoPropiedad,
            view.TotalHabitaciones,
            view.HabitacionesDisponibles,
            view.Habitaciones.Select(MiHabitacionResponse.FromView).ToArray());
}
