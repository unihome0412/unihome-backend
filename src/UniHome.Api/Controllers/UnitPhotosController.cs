using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Units.Photos;
using UniHome.Domain.Properties;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/fotos")]
public sealed class UnitPhotosController : AuthenticatedControllerBase
{
    private readonly IUnitRepository _units;
    private readonly IUnitPhotoStorage _storage;

    public UnitPhotosController(IUnitRepository units, IUnitPhotoStorage storage)
    {
        _units = units;
        _storage = storage;
    }

    /// <summary>
    /// Entrega autorizada de una fotografía. Las unidades públicas pueden
    /// entregarse sin sesión; los borradores solo a su propietario. La URL de
    /// Cloudinary y las credenciales nunca salen del backend.
    /// </summary>
    [HttpGet("{photoRef}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Read(
        string photoRef,
        CancellationToken cancellationToken)
    {
        if (!PhotoReference.TryDecode(photoRef, out var storageKey))
        {
            return CreateProblemResult(
                StatusCodes.Status404NotFound,
                "La fotografía no existe.");
        }

        var unit = await _units
            .FindByPhotoStorageKeyAsync(storageKey, cancellationToken)
            .ConfigureAwait(false);

        if (unit is null)
        {
            return CreateProblemResult(
                StatusCodes.Status404NotFound,
                "La fotografía no existe.");
        }

        var isPublic = unit.PublicationState == PublicationState.Aprobada;

        if (!isPublic)
        {
            if (!TryGetAccountId(out var accountId))
            {
                return CreateProblemResult(
                    StatusCodes.Status401Unauthorized,
                    "La sesión no es válida.");
            }

            if (unit.OwnerAccountId != accountId)
            {
                return CreateProblemResult(
                    StatusCodes.Status404NotFound,
                    "La fotografía no existe.");
            }
        }

        var photo = await _storage
            .ReadAsync(storageKey, cancellationToken)
            .ConfigureAwait(false);

        if (photo is null)
        {
            return CreateProblemResult(
                StatusCodes.Status404NotFound,
                "La fotografía no existe.");
        }

        Response.Headers["Cache-Control"] = isPublic
            ? "public, max-age=300"
            : "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(photo.Content, photo.ContentType, enableRangeProcessing: false);
    }
}
