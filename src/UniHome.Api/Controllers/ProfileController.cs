using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Profiles;
using UniHome.Api.Errors;
using UniHome.Application.Profiles.GetProfile;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/perfil")]
[Authorize]
public sealed class ProfileController : ControllerBase
{
    private readonly GetProfileService _getProfile;

    public ProfileController(GetProfileService getProfile)
    {
        _getProfile = getProfile;
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

        // universidadActual permanece null: M01 no persiste una asociación
        // cuenta-universidad y M03 solo asocia UniversidadId a propiedades y
        // unidades. No se inventa una relación ni se toma la universidad de una
        // propiedad del titular. La dependencia funcional es HU-087 (Sprint 8).
        return Ok(new PerfilResponse(
            profile.Account.FullName,
            profile.Account.Email.Value,
            profile.Account.BirthDate,
            profile.Account.Gender,
            UniversidadActual: null,
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
