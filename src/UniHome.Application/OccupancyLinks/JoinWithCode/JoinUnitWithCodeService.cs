using UniHome.Application.Identity.Abstractions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.OccupancyLinks;

namespace UniHome.Application.OccupancyLinks.JoinWithCode;

/// <summary>
/// EP-M09-02 (HU-049): vincula al solicitante como inquilino de una unidad a
/// partir de un código de invitación vigente. El código no se consume: sigue
/// válido para otros inquilinos mientras la unidad tenga lugares y el código no
/// haya vencido. Reingresar con el mismo código reactiva el vínculo anterior.
/// </summary>
public sealed class JoinUnitWithCodeService
{
    private readonly IUnitInvitationRepository _invitations;
    private readonly IUnitRepository _units;
    private readonly IOccupancyLinkRepository _links;
    private readonly UnitAvailabilityRecalculator _recalculator;
    private readonly ISystemClock _clock;

    public JoinUnitWithCodeService(
        IUnitInvitationRepository invitations,
        IUnitRepository units,
        IOccupancyLinkRepository links,
        UnitAvailabilityRecalculator recalculator,
        ISystemClock clock)
    {
        _invitations = invitations;
        _units = units;
        _links = links;
        _recalculator = recalculator;
        _clock = clock;
    }

    public async Task<JoinUnitWithCodeResult> JoinAsync(
        Guid tenantAccountId,
        string? code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return JoinUnitWithCodeResult.ValidationFailed("codigo", "El código es obligatorio.");
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        var invitation = await _invitations
            .FindByCodeAsync(normalizedCode, cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null)
        {
            return JoinUnitWithCodeResult.InvalidCode();
        }

        var now = _clock.UtcNow;

        if (invitation.IsExpired(now))
        {
            return JoinUnitWithCodeResult.ExpiredCode();
        }

        var unit = await _units.FindByIdAsync(invitation.UnitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return JoinUnitWithCodeResult.InvalidCode();
        }

        if (unit.OwnerAccountId == tenantAccountId)
        {
            return JoinUnitWithCodeResult.SelfJoinForbidden();
        }

        var existing = await _links
            .FindAsync(tenantAccountId, unit.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null && existing.Status == OccupancyStatus.Activo)
        {
            return JoinUnitWithCodeResult.Linked(
                new OccupancyLinkView(unit.Id, existing.Status, existing.JoinedAt));
        }

        var activeCount = await _links.CountActiveAsync(unit.Id, cancellationToken).ConfigureAwait(false);

        if (activeCount >= unit.Places)
        {
            return JoinUnitWithCodeResult.UnitFull();
        }

        var link = existing ?? OccupancyLink.CreateActive(tenantAccountId, unit.Id, now);

        if (existing is not null)
        {
            link.Reactivate(now);
        }

        await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
        await _recalculator.RecomputeAsync(unit, cancellationToken).ConfigureAwait(false);

        return JoinUnitWithCodeResult.Linked(new OccupancyLinkView(unit.Id, link.Status, link.JoinedAt));
    }
}
