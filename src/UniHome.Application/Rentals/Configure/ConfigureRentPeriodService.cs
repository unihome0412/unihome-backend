using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Profiles;
using UniHome.Application.Rentals.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Rentals;

namespace UniHome.Application.Rentals.Configure;

/// <summary>
/// EP-M10-01 (HU-050/051/052): el titular define o redefine la renta total,
/// los conceptos de cobro y el día de corte de un periodo. Nunca hereda del
/// periodo anterior: siempre usa los valores explícitos de la solicitud y
/// reinicia la división a pendiente (ver <see cref="RentPeriod.SetConfiguration"/>).
/// </summary>
public sealed class ConfigureRentPeriodService
{
    private readonly IUnitRepository _units;
    private readonly IRentPeriodRepository _rentPeriods;
    private readonly PublishingEligibilityService _eligibility;
    private readonly ISystemClock _clock;

    public ConfigureRentPeriodService(
        IUnitRepository units,
        IRentPeriodRepository rentPeriods,
        PublishingEligibilityService eligibility,
        ISystemClock clock)
    {
        _units = units;
        _rentPeriods = rentPeriods;
        _eligibility = eligibility;
        _clock = clock;
    }

    public async Task<OperationResult<RentPeriod>> ConfigureAsync(
        Guid ownerAccountId,
        Guid unitId,
        string periodo,
        decimal totalMxn,
        int cutoffDay,
        IReadOnlyCollection<RentConcept>? concepts,
        CancellationToken cancellationToken = default)
    {
        if (concepts is null)
        {
            return OperationResult<RentPeriod>.ValidationFailed(
                "conceptos",
                "La lista de conceptos es obligatoria.");
        }

        if (!await _eligibility.IsEligibleAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<RentPeriod>.Forbidden();
        }

        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<RentPeriod>.NotFound();
        }

        if (unit.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<RentPeriod>.Forbidden();
        }

        var now = _clock.UtcNow;

        try
        {
            var existing = await _rentPeriods
                .FindAsync(unitId, periodo, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.SetConfiguration(totalMxn, cutoffDay, concepts, now);
                await _rentPeriods.SaveAsync(existing, cancellationToken).ConfigureAwait(false);
                return OperationResult<RentPeriod>.Success(existing);
            }

            var created = RentPeriod.Configure(unitId, periodo, totalMxn, cutoffDay, concepts, now);
            await _rentPeriods.SaveAsync(created, cancellationToken).ConfigureAwait(false);
            return OperationResult<RentPeriod>.Success(created);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<RentPeriod>.ValidationFailed("renta", exception.Message);
        }
    }
}
