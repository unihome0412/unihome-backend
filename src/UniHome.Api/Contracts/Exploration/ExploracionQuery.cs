using Microsoft.AspNetCore.Mvc;
using UniHome.Domain.Units;

namespace UniHome.Api.Contracts.Exploration;

public sealed class ExploracionQuery
{
    [FromQuery(Name = "universidadId")]
    public Guid? UniversidadId { get; set; }

    [FromQuery(Name = "distanciaMaxKm")]
    public decimal? DistanciaMaxKm { get; set; }

    [FromQuery(Name = "precioMaxMxn")]
    public decimal? PrecioMaxMxn { get; set; }

    [FromQuery(Name = "soloIndividual")]
    public bool? SoloIndividual { get; set; }

    [ModelBinder(BinderType = typeof(ForWhomQueryModelBinder))]
    [FromQuery(Name = "paraQuien")]
    public ForWhom? ParaQuien { get; set; }

    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;
}
