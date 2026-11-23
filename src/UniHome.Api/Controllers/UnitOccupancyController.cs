using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Common;
using UniHome.Api.Contracts.OccupancyLinks;
using UniHome.Api.Contracts.Units;
using UniHome.Application.OccupancyLinks.GetInvitation;
using UniHome.Application.OccupancyLinks.JoinWithCode;
using UniHome.Application.OccupancyLinks.LeaveUnit;
using UniHome.Application.OccupancyLinks.ListTenants;
using UniHome.Application.OccupancyLinks.RemoveTenant;
using UniHome.Application.OccupancyLinks.SetMaintenance;

namespace UniHome.Api.Controllers;

/// <summary>
/// EP-M09 (HU-048/049/093/094/095): invitaciones, vínculo con código,
/// mantenimiento y baja/abandono de ocupación.
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class UnitOccupancyController : AuthenticatedControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;

    private readonly GetUnitInvitationService _getInvitation;
    private readonly JoinUnitWithCodeService _joinWithCode;
    private readonly SetUnitMaintenanceService _setMaintenance;
    private readonly RemoveTenantService _removeTenant;
    private readonly LeaveUnitService _leaveUnit;
    private readonly ListUnitTenantsService _listTenants;

    public UnitOccupancyController(
        GetUnitInvitationService getInvitation,
        JoinUnitWithCodeService joinWithCode,
        SetUnitMaintenanceService setMaintenance,
        RemoveTenantService removeTenant,
        LeaveUnitService leaveUnit,
        ListUnitTenantsService listTenants)
    {
        _getInvitation = getInvitation;
        _joinWithCode = joinWithCode;
        _setMaintenance = setMaintenance;
        _removeTenant = removeTenant;
        _leaveUnit = leaveUnit;
        _listTenants = listTenants;
    }

    /// <summary>
    /// EP-M09-06 (HU-094): inquilinos activos de una unidad propia, con nombre y
    /// fecha de vínculo; nunca correo ni teléfono. Permite obtener el
    /// <c>tenantAccountId</c> antes de dar de baja a alguien.
    /// </summary>
    [HttpGet("unidades/{unitId:guid}/inquilinos")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<InquilinoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListTenants(Guid unitId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _listTenants
            .ListAsync(accountId, unitId, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(value.Select(InquilinoResponse.FromView).ToArray()),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo completar la operación.");
    }

    /// <summary>EP-M09-01 (HU-048): código de invitación vigente de una unidad propia.</summary>
    [HttpGet("unidades/{unitId:guid}/invitacion")]
    [Authorize]
    [ProducesResponseType(typeof(InvitacionUnidadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetInvitation(Guid unitId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _getInvitation
            .GetAsync(accountId, unitId, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(InvitacionUnidadResponse.FromView(value)),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo generar un código de invitación. Intente de nuevo.");
    }

    /// <summary>EP-M09-02 (HU-049): vincula al solicitante como inquilino mediante un código.</summary>
    [HttpPost("unidades/unirse")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(VinculoOcupacionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> JoinWithCode(
        [FromBody] UnirseConCodigoRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _joinWithCode
            .JoinAsync(accountId, request.Codigo, cancellationToken)
            .ConfigureAwait(false);

        return result.Status switch
        {
            JoinUnitWithCodeStatus.Linked => Ok(VinculoOcupacionResponse.FromView(result.Value!)),
            JoinUnitWithCodeStatus.ValidationFailed => CreateValidationResult(result.ValidationErrors),
            JoinUnitWithCodeStatus.InvalidCode => CreateProblemResult(
                StatusCodes.Status400BadRequest,
                "El código de invitación no es válido."),
            JoinUnitWithCodeStatus.ExpiredCode => CreateProblemResult(
                StatusCodes.Status410Gone,
                "El código de invitación venció. Solicita uno nuevo al propietario."),
            JoinUnitWithCodeStatus.SelfJoinForbidden => CreateProblemResult(
                StatusCodes.Status403Forbidden,
                "No puedes unirte a tu propia unidad."),
            JoinUnitWithCodeStatus.UnitFull => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "La unidad ya no tiene lugares disponibles."),
            JoinUnitWithCodeStatus.ConcurrentUpdate => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "El vínculo fue modificado por otra operación. Intente de nuevo."),
            _ => CreateProblemResult(
                StatusCodes.Status500InternalServerError,
                "No se pudo completar la operación.")
        };
    }

    /// <summary>EP-M09-03 (HU-093): marca o desmarca el mantenimiento de una unidad propia.</summary>
    [HttpPut("unidades/{unitId:guid}/mantenimiento")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(UnidadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetMaintenance(
        Guid unitId,
        [FromBody] MantenimientoRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _setMaintenance
            .SetAsync(accountId, unitId, request.EnMantenimiento, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(UnidadResponse.FromDomain(value)),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "La unidad fue modificada por otra operación.");
    }

    /// <summary>EP-M09-04 (HU-094): da de baja a un inquilino de una unidad propia.</summary>
    [HttpDelete("unidades/{unitId:guid}/inquilinos/{tenantAccountId:guid}")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RemoveTenant(
        Guid unitId,
        Guid tenantAccountId,
        [FromBody] ConfirmacionRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _removeTenant
            .RemoveAsync(accountId, unitId, tenantAccountId, request.Confirmacion, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            _ => NoContent(),
            notFoundDetail: "El inquilino no está vinculado a esta unidad.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo completar la operación.");
    }

    /// <summary>EP-M09-05 (HU-095): el propio inquilino deja la unidad.</summary>
    [HttpDelete("unidades/{unitId:guid}/mi-vinculo")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> LeaveUnit(
        Guid unitId,
        [FromBody] ConfirmacionRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _leaveUnit
            .LeaveAsync(accountId, unitId, request.Confirmacion, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            _ => NoContent(),
            notFoundDetail: "No tienes un vínculo activo con esta unidad.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo completar la operación.");
    }
}
