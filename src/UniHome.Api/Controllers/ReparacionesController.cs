using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Reparaciones;
using UniHome.Application.Reparaciones;
using UniHome.Application.Units.Photos;

namespace UniHome.Api.Controllers;

/// <summary>
/// EP-M11 (HU-062/063/064/065): reportes de reparación de una unidad.
/// </summary>
[ApiController]
[Route("api/v1/unidades/{unitId:guid}/reparaciones")]
public sealed class ReparacionesController : AuthenticatedControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;
    private const long MaxRequestBytesWithPhoto = UnitPhotoValidator.MaxRequestBytes;

    private readonly CreateReparacionService _create;
    private readonly ListReparacionesService _list;
    private readonly ChangeReparacionStatusService _changeStatus;

    public ReparacionesController(
        CreateReparacionService create,
        ListReparacionesService list,
        ChangeReparacionStatusService changeStatus)
    {
        _create = create;
        _list = list;
        _changeStatus = changeStatus;
    }

    /// <summary>EP-M11-01 (HU-062/063): crea un reporte con título, descripción y foto opcional.</summary>
    [HttpPost]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytesWithPhoto)]
    [ProducesResponseType(typeof(ReparacionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        Guid unitId,
        [FromForm] CrearReparacionRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytesWithPhoto)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        if (request.Foto is not null && request.Foto.Length > UnitPhotoValidator.MaxBytesPerPhoto)
        {
            return CreateValidationResult(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["foto"] =
                    [
                        $"La fotografía debe pesar como máximo " +
                        $"{UnitPhotoValidator.MaxBytesPerPhoto / (1024 * 1024)} MB."
                    ]
                });
        }

        UnitPhotoUpload? upload = null;

        if (request.Foto is not null)
        {
            await using var stream = request.Foto.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            upload = new UnitPhotoUpload(request.Foto.FileName, request.Foto.ContentType, memory.ToArray());
        }

        var result = await _create
            .CreateAsync(accountId, unitId, request.Titulo, request.Descripcion, upload, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => StatusCode(StatusCodes.Status201Created, ReparacionResponse.FromDomain(value)),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo completar la operación.");
    }

    /// <summary>EP-M11-02 (HU-064): reportes de la unidad, con estado, quién los levantó y su detalle.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<ReparacionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List(Guid unitId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _list.ListAsync(accountId, unitId, cancellationToken).ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(value.Select(ReparacionResponse.FromView).ToArray()),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo completar la operación.");
    }

    /// <summary>EP-M11-03 (HU-065/066): el propietario cambia el estado; notifica a los demás con acceso a la unidad.</summary>
    [HttpPut("{reparacionId:guid}/estado")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(ReparacionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ChangeStatus(
        Guid unitId,
        Guid reparacionId,
        [FromBody] CambiarEstadoReparacionRequest request,
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

        var result = await _changeStatus
            .ChangeStatusAsync(accountId, unitId, reparacionId, request.Estado, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(ReparacionResponse.FromDomain(value)),
            notFoundDetail: "El reporte no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo completar la operación.");
    }
}
