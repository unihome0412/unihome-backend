using UniHome.Application.Identity.Abstractions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Rentals.Abstractions;
using UniHome.Domain.Rentals;

namespace UniHome.Application.Rentals;

/// <summary>
/// EP-M10: resuelve el periodo vigente de una unidad. Si no existe, lo hereda
/// del periodo anterior más reciente (renta total, conceptos y día de corte
/// siempre; división y cuotas solo si los inquilinos activos no cambiaron) y
/// persiste la copia heredada. Si no hay periodo anterior, no hay nada que
/// heredar: el llamador interpreta null como "la renta no está configurada".
/// No se usa desde la configuración explícita (HU-050/051/052), que siempre
/// define valores nuevos y reinicia la división.
/// </summary>
public sealed class RentPeriodResolver
{
    private readonly IRentPeriodRepository _rentPeriods;
    private readonly IOccupancyLinkRepository _links;
    private readonly ISystemClock _clock;

    public RentPeriodResolver(
        IRentPeriodRepository rentPeriods,
        IOccupancyLinkRepository links,
        ISystemClock clock)
    {
        _rentPeriods = rentPeriods;
        _links = links;
        _clock = clock;
    }

    public async Task<RentPeriod?> ResolveAsync(
        Guid unitId,
        string periodo,
        CancellationToken cancellationToken = default)
    {
        var existing = await _rentPeriods
            .FindAsync(unitId, periodo, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var previous = await _rentPeriods
            .FindMostRecentBeforeAsync(unitId, periodo, cancellationToken)
            .ConfigureAwait(false);

        if (previous is null)
        {
            return null;
        }

        var activeLinks = await _links
            .ListActiveByUnitAsync(unitId, cancellationToken)
            .ConfigureAwait(false);
        var activeTenantIds = activeLinks.Select(link => link.TenantAccountId).ToArray();

        var inherited = RentPeriod.InheritFrom(previous, periodo, activeTenantIds, _clock.UtcNow);
        await _rentPeriods.SaveAsync(inherited, cancellationToken).ConfigureAwait(false);
        return inherited;
    }
}
