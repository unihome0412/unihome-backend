using UniHome.Domain.Rentals;

namespace UniHome.Application.Rentals.Abstractions;

public interface IRentPeriodRepository
{
    Task<RentPeriod?> FindAsync(
        Guid unitId,
        string periodo,
        CancellationToken cancellationToken = default);

    /// <summary>El periodo más reciente de la unidad con periodo estrictamente anterior al dado.</summary>
    Task<RentPeriod?> FindMostRecentBeforeAsync(
        Guid unitId,
        string periodo,
        CancellationToken cancellationToken = default);

    /// <summary>EP-M10 (HU-058): todos los periodos ya registrados de la unidad, para construir el historial.</summary>
    Task<IReadOnlyList<RentPeriod>> ListByUnitAsync(
        Guid unitId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(RentPeriod period, CancellationToken cancellationToken = default);
}
