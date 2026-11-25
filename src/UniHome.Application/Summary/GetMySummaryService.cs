using System.Globalization;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Rentals;
using UniHome.Application.Rentals.Abstractions;
using UniHome.Application.Reparaciones.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Reparaciones;

namespace UniHome.Application.Summary;

/// <summary>
/// EP-M05/M10/M11 (HU-034): resumen del titular para la pantalla de Resumen:
/// rentas registradas del mes en curso, número de propiedades y reparaciones
/// abiertas (Pendiente o En proceso) en sus unidades. Una cuenta sin
/// propiedades recibe el resumen en ceros, nunca un error.
/// </summary>
public sealed class GetMySummaryService
{
    private readonly IUnitRepository _units;
    private readonly IPropertyRepository _properties;
    private readonly RentPeriodResolver _resolver;
    private readonly IInformativePaymentRepository _payments;
    private readonly IReparacionRepository _reparaciones;
    private readonly ISystemClock _clock;

    public GetMySummaryService(
        IUnitRepository units,
        IPropertyRepository properties,
        RentPeriodResolver resolver,
        IInformativePaymentRepository payments,
        IReparacionRepository reparaciones,
        ISystemClock clock)
    {
        _units = units;
        _properties = properties;
        _resolver = resolver;
        _payments = payments;
        _reparaciones = reparaciones;
        _clock = clock;
    }

    public async Task<MySummaryView> GetAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var periodo = DateOnly
            .FromDateTime(_clock.UtcNow.UtcDateTime)
            .ToString("yyyy-MM", CultureInfo.InvariantCulture);

        var ownedUnits = await _units.ListByOwnerAsync(accountId, cancellationToken).ConfigureAwait(false);
        var properties = await _properties.ListByOwnerAsync(accountId, cancellationToken).ConfigureAwait(false);

        var rentasDelMes = 0m;
        var reparacionesAbiertas = 0;

        foreach (var unit in ownedUnits)
        {
            var period = await _resolver.ResolveAsync(unit.Id, periodo, cancellationToken).ConfigureAwait(false);

            if (period is not null)
            {
                var payments = await _payments
                    .ListByRentPeriodAsync(period.Id, cancellationToken)
                    .ConfigureAwait(false);
                rentasDelMes += payments.Sum(payment => payment.AmountMxn);
            }

            var reports = await _reparaciones
                .ListByUnitAsync(unit.Id, cancellationToken)
                .ConfigureAwait(false);
            reparacionesAbiertas += reports.Count(report => report.Status != ReparacionEstado.Atendido);
        }

        return new MySummaryView(periodo, rentasDelMes, properties.Count, reparacionesAbiertas);
    }
}

public sealed record MySummaryView(
    string Periodo,
    decimal RentasRegistradasDelMesMxn,
    int NumeroPropiedades,
    int ReparacionesAbiertas);
