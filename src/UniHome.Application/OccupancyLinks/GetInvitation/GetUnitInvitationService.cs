using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.OccupancyLinks.Exceptions;
using UniHome.Application.Profiles;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.OccupancyLinks;

namespace UniHome.Application.OccupancyLinks.GetInvitation;

/// <summary>
/// EP-M09-01 (HU-048): código de invitación vigente de una unidad propia, con su
/// fecha de vigencia. Si no existe uno o venció, genera uno nuevo; los anteriores
/// no se borran, quedan vencidos.
/// </summary>
public sealed class GetUnitInvitationService
{
    private const int MaxGenerationAttempts = 5;

    private readonly IUnitRepository _units;
    private readonly IUnitInvitationRepository _invitations;
    private readonly IUnitInvitationCodeGenerator _codeGenerator;
    private readonly PublishingEligibilityService _eligibility;
    private readonly ISystemClock _clock;

    public GetUnitInvitationService(
        IUnitRepository units,
        IUnitInvitationRepository invitations,
        IUnitInvitationCodeGenerator codeGenerator,
        PublishingEligibilityService eligibility,
        ISystemClock clock)
    {
        _units = units;
        _invitations = invitations;
        _codeGenerator = codeGenerator;
        _eligibility = eligibility;
        _clock = clock;
    }

    public async Task<OperationResult<UnitInvitationView>> GetAsync(
        Guid ownerAccountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        if (!await _eligibility.IsEligibleAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<UnitInvitationView>.Forbidden();
        }

        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<UnitInvitationView>.NotFound();
        }

        if (unit.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<UnitInvitationView>.Forbidden();
        }

        var now = _clock.UtcNow;
        var latest = await _invitations
            .FindLatestByUnitAsync(unitId, cancellationToken)
            .ConfigureAwait(false);

        if (latest is not null && !latest.IsExpired(now))
        {
            return OperationResult<UnitInvitationView>.Success(
                new UnitInvitationView(latest.Code, latest.ExpiresAt));
        }

        for (var attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var invitation = UnitInvitation.Create(unitId, _codeGenerator.Generate(), now);

            try
            {
                await _invitations.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
                return OperationResult<UnitInvitationView>.Success(
                    new UnitInvitationView(invitation.Code, invitation.ExpiresAt));
            }
            catch (DuplicateInvitationCodeException)
            {
                // Colisión de código, tan improbable como una de GUID; se reintenta.
            }
        }

        return OperationResult<UnitInvitationView>.Conflict();
    }
}

public sealed record UnitInvitationView(string Codigo, DateTimeOffset VigenciaHasta);
