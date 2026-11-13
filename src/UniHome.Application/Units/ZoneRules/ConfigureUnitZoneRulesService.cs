using UniHome.Application.Common;
using UniHome.Application.Profiles;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Units;

namespace UniHome.Application.Units.ZoneRules;

/// <summary>
/// EP-M05-05 (HU-082/HU-083): información de zona y reglas de convivencia de una
/// unidad propia. No publica ni cambia el estado de la unidad.
/// </summary>
public sealed class ConfigureUnitZoneRulesService
{
    private readonly IUnitRepository _units;
    private readonly PublishingEligibilityService _eligibility;

    public ConfigureUnitZoneRulesService(
        IUnitRepository units,
        PublishingEligibilityService eligibility)
    {
        _units = units;
        _eligibility = eligibility;
    }

    public async Task<OperationResult<Unit>> ConfigureAsync(
        Guid ownerAccountId,
        Guid unitId,
        ConfigureUnitZoneRulesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await _eligibility.IsEligibleAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<Unit>.Forbidden();
        }

        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<Unit>.NotFound();
        }

        if (unit.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<Unit>.Forbidden();
        }

        try
        {
            unit.ConfigureZoneRules(command.InformacionZona, command.ReglasConvivencia);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<Unit>.ValidationFailed("zonaReglas", exception.Message);
        }

        var updated = await _units
            .UpdateZoneRulesAsync(
                unit.Id,
                unit.PropertyId,
                unit.ZoneInfo,
                unit.Rules,
                cancellationToken)
            .ConfigureAwait(false);

        return updated
            ? OperationResult<Unit>.Success(unit)
            : OperationResult<Unit>.NotFound();
    }
}
