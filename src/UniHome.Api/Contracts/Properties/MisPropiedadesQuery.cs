using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Common;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.Api.Contracts.Properties;

public sealed class MisPropiedadesQuery
{
    [ModelBinder(BinderType = typeof(EnumQueryModelBinder<PropertyType>))]
    [FromQuery(Name = "tipoPropiedad")]
    public PropertyType? TipoPropiedad { get; set; }

    [ModelBinder(BinderType = typeof(EnumQueryModelBinder<UnitAvailability>))]
    [FromQuery(Name = "estadoHabitacion")]
    public UnitAvailability? EstadoHabitacion { get; set; }
}
