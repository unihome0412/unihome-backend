using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Profiles;
using UniHome.Api.Contracts.Universities;
using UniHome.Api.Errors;
using UniHome.Application.Common;
using UniHome.Application.Profiles;
using UniHome.Application.Profiles.GetProfile;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/perfil")]
[Authorize]
public sealed class ProfileController : ControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;

    private readonly GetProfileService _getProfile;
    private readonly ChangeMyUniversityService _changeUniversity;

    public ProfileController(GetProfileService getProfile, ChangeMyUniversityService changeUniversity)
    {
        _getProfile = getProfile;
        _changeUniversity = changeUniversity;
    }

    /// <summary>
    /// EP-M02-01 (HU-002/HU-008): perfil del titular autenticado. Entrega los
    /// datos personales y los indicadores de navegación calculados en servidor.
    /// </summary>
    [HttpGet("mi-perfil")]
    [ProducesResponseType(typeof(PerfilResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var accountClaim = User.FindFirst("sub")?.Value;

        if (!Guid.TryParseExact(accountClaim, "N", out var accountId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var profile = await _getProfile
            .GetAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        // HU-087: universidadActual refleja Account.CurrentUniversityId, fijado
        // desde este mismo perfil o desde el mapa con PUT mi-universidad. Null
        // hasta que la cuenta la elija por primera vez.
        return Ok(new PerfilResponse(
            profile.Account.FullName,
            profile.Account.Email.Value,
            profile.Account.BirthDate,
            profile.Account.Gender,
            UniversidadActual: profile.CurrentUniversity is null
                ? null
                : UniversidadResumenResponse.FromDomain(profile.CurrentUniversity),
            profile.Label,
            profile.IsLinkedToUnit,
            profile.CanPublish,
            profile.FirstPublicationState));
    }

    /// <summary>
    /// EP-M02/M03 (HU-087): fija la universidad actual, desde el perfil o
    /// desde el mapa (mismo endpoint para ambos puntos de entrada del cliente).
    /// </summary>
    [HttpPut("mi-universidad")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(PerfilResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ChangeMyUniversity(
        [FromBody] CambiarMiUniversidadRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        var accountClaim = User.FindFirst("sub")?.Value;

        if (!Guid.TryParseExact(accountClaim, "N", out var accountId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _changeUniversity
            .ChangeAsync(accountId, request.UniversidadId, cancellationToken)
            .ConfigureAwait(false);

        if (result.Status == OperationStatus.ValidationFailed)
        {
            var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();

            foreach (var (field, messages) in result.ValidationErrors)
            {
                foreach (var message in messages)
                {
                    modelState.AddModelError(field, message);
                }
            }

            return new BadRequestObjectResult(ApiProblemDetails.CreateValidation(HttpContext, modelState))
            {
                ContentTypes = { ApiProblemDetailsWriter.ContentType }
            };
        }

        if (result.Status != OperationStatus.Success)
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        var profile = await _getProfile.GetAsync(accountId, cancellationToken).ConfigureAwait(false);

        if (profile is null)
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        return Ok(new PerfilResponse(
            profile.Account.FullName,
            profile.Account.Email.Value,
            profile.Account.BirthDate,
            profile.Account.Gender,
            UniversidadActual: profile.CurrentUniversity is null
                ? null
                : UniversidadResumenResponse.FromDomain(profile.CurrentUniversity),
            profile.Label,
            profile.IsLinkedToUnit,
            profile.CanPublish,
            profile.FirstPublicationState));
    }

    private ObjectResult CreateProblemResult(int statusCode, string detail) =>
        new(ApiProblemDetails.Create(HttpContext, statusCode, detail: detail))
        {
            StatusCode = statusCode,
            ContentTypes = { ApiProblemDetailsWriter.ContentType }
        };
}
