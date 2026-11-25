using UniHome.Domain.Rentals;

namespace UniHome.Api.Contracts.Rentals;

public sealed record ConceptoResponse(RentConceptName Nombre, bool EsFijo);

public sealed record CuotaResponse(Guid InquilinoAccountId, decimal MontoMxn);

public sealed record RentaPeriodoResponse(
    Guid UnitId,
    string Periodo,
    decimal RentaTotalMxn,
    int DiaCorte,
    DateOnly FechaCorte,
    IReadOnlyList<ConceptoResponse> Conceptos,
    RentSplitMethod? MetodoDivision,
    IReadOnlyList<CuotaResponse> Cuotas,
    string Aviso)
{
    public static RentaPeriodoResponse FromDomain(RentPeriod period) =>
        new(
            period.UnitId,
            period.Periodo,
            period.TotalMxn,
            period.CutoffDay,
            RentPeriod.ComputeCutoffDate(period.Periodo, period.CutoffDay),
            period.Concepts.Select(concept => new ConceptoResponse(concept.Nombre, concept.EsFijo)).ToArray(),
            period.SplitMethod,
            period.Shares.Select(share => new CuotaResponse(share.TenantAccountId, share.AmountMxn)).ToArray(),
            RentNotices.PagosInformativos);
}
