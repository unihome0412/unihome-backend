using UniHome.Domain.Units;

namespace UniHome.Api.Contracts.Exploration;

public sealed class ExploracionQuery
{
    public Guid? UniversidadId { get; set; }
    public decimal? DistanciaMaxKm { get; set; }
    public decimal? PrecioMaxMxn { get; set; }
    public bool? SoloIndividual { get; set; }
    public ForWhom? ParaQuien { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
