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
            // Idempotente: el inquilino ya está contado en lugaresOcupados, no se
            // reserva un lugar adicional.
            return Linked(unit.Id, existing);
        }

        // CAS de un solo documento sobre la propia unidad: dos solicitudes con un
        // solo lugar libre no pueden reservar ambas, a diferencia de contar los
        // vínculos de ocupación (sujeto a condición de carrera entre el conteo y
        // la inserción).
        var reserved = await _units
            .TryReserveSlotAsync(unit.Id, unit.PropertyId, cancellationToken)
            .ConfigureAwait(false);

        if (reserved is null)
        {
            // Otra solicitud del mismo inquilino pudo activar el vínculo mientras
            // esta esperaba el CAS del cupo. En ese caso el resultado correcto es
            // idempotente y no un falso "unidad llena".
            var active = await _links
                .FindAsync(tenantAccountId, unit.Id, cancellationToken)
                .ConfigureAwait(false);

            return active is not null && active.Status == OccupancyStatus.Activo
                ? Linked(unit.Id, active)
                : JoinUnitWithCodeResult.UnitFull();
        }

        // Se usa un candidato nuevo en vez de mutar el vínculo leído: la
        // reactivación real la decide la transición atómica del repositorio.
        var link = OccupancyLink.CreateActive(tenantAccountId, unit.Id, now);

        try
        {
            // Reservar y activar son dos operaciones atómicas separadas. Si la
            // activación pierde la carrera, se libera la reserva de esta solicitud
            // para que el contador no quede inflado; nunca se libera dos veces un
            // lugar ajeno.
            var activated = await _links
                .TryActivateAsync(link, cancellationToken)
                .ConfigureAwait(false);

            if (!activated)
            {
                await _units.ReleaseSlotAsync(unit.Id, unit.PropertyId, cancellationToken).ConfigureAwait(false);
                var active = await _links
                    .FindAsync(tenantAccountId, unit.Id, cancellationToken)
                    .ConfigureAwait(false);

                return active is not null && active.Status == OccupancyStatus.Activo
                    ? Linked(unit.Id, active)
                    : JoinUnitWithCodeResult.ConcurrentUpdate();
            }
        }
        catch
        {
            // Compensación: el lugar se reservó pero el vínculo no se pudo
            // persistir; se libera para no perder el cupo definitivamente.
            await _units.ReleaseSlotAsync(unit.Id, unit.PropertyId, cancellationToken).ConfigureAwait(false);
            throw;
        }

        await _recalculator
            .RecomputeAsync(unit, reserved.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return Linked(unit.Id, link);
    }

    private static JoinUnitWithCodeResult Linked(Guid unitId, OccupancyLink link) =>
        JoinUnitWithCodeResult.Linked(
            new OccupancyLinkView(unitId, link.Status, link.JoinedAt));
}
