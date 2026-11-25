using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Profiles;
using UniHome.Application.Rentals.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Rentals;

namespace UniHome.Application.Rentals.Payments;

/// <summary>
/// EP-M10-03 (HU-055): el titular registra un pago informativo recibido de un
/// inquilino con vínculo activo. UniHome no procesa el dinero (HU-090); es
/// solo una bitácora. Resuelve el periodo (existente o heredado del anterior
/// más reciente) antes de registrar, igual que la división.
/// </summary>
public sealed class RegisterRentPaymentService
{
    private readonly IUnitRepository _units;
    private readonly IOccupancyLinkRepository _links;
    private readonly IInformativePaymentRepository _payments;
    private readonly RentPeriodResolver _resolver;
    private readonly PublishingEligibilityService _eligibility;
    private readonly ISystemClock _clock;

    public RegisterRentPaymentService(
        IUnitRepository units,
        IOccupancyLinkRepository links,
        IInformativePaymentRepository payments,
        RentPeriodResolver resolver,
        PublishingEligibilityService eligibility,
        ISystemClock clock)
    {
        _units = units;
        _links = links;
        _payments = payments;
        _resolver = resolver;
        _eligibility = eligibility;
        _clock = clock;
    }

    public async Task<OperationResult<InformativePayment>> RegisterAsync(
        Guid ownerAccountId,
        Guid unitId,
        string periodo,
        Guid tenantAccountId,
        decimal amountMxn,
        DateOnly receivedOn,
        CancellationToken cancellationToken = default)
    {
        if (!await _eligibility.IsEligibleAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<InformativePayment>.Forbidden();
        }

        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<InformativePayment>.NotFound();
        }

        if (unit.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<InformativePayment>.Forbidden();
        }

        var period = await _resolver.ResolveAsync(unitId, periodo, cancellationToken).ConfigureAwait(false);

        if (period is null)
        {
            return OperationResult<InformativePayment>.NotFound();
        }

        var activeLinks = await _links
            .ListActiveByUnitAsync(unitId, cancellationToken)
            .ConfigureAwait(false);

        if (!activeLinks.Any(link => link.TenantAccountId == tenantAccountId))
        {
            return OperationResult<InformativePayment>.ValidationFailed(
                "inquilinoAccountId",
                "El inquilino debe tener un vínculo activo en la unidad.");
        }

        try
        {
            var payment = InformativePayment.Register(
                period.Id, tenantAccountId, amountMxn, receivedOn, _clock.UtcNow);
            await _payments.SaveAsync(payment, cancellationToken).ConfigureAwait(false);
            return OperationResult<InformativePayment>.Success(payment);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<InformativePayment>.ValidationFailed("pago", exception.Message);
        }
    }
}
