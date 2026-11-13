using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UniHome.Api.Contracts.Universities;
using UniHome.Api.Errors;
using UniHome.Application.Universities.SearchUniversities;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/universidades")]
public sealed class UniversitiesController : ControllerBase
{
    private readonly SearchUniversitiesService _searchUniversities;

    public UniversitiesController(SearchUniversitiesService searchUniversities)
    {
        _searchUniversities = searchUniversities;
    }

    /// <summary>
    /// EP-M03-01 (HU-007/HU-075): busca hasta 10 universidades activas por nombre
    /// o campus, sin distinguir mayúsculas ni acentos. Es pública.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(
        typeof(IReadOnlyList<UniversidadResumenResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Search(
        [FromQuery] string? buscar,
        CancellationToken cancellationToken)
    {
        var result = await _searchUniversities
            .SearchAsync(buscar, cancellationToken)
            .ConfigureAwait(false);

        if (result.ValidationErrors.Count > 0)
        {
            var modelState = new ModelStateDictionary();

            foreach (var (field, messages) in result.ValidationErrors)
            {
                foreach (var message in messages)
                {
                    modelState.AddModelError(field, message);
                }
            }

            return new BadRequestObjectResult(
                ApiProblemDetails.CreateValidation(HttpContext, modelState))
            {
                ContentTypes = { ApiProblemDetailsWriter.ContentType }
            };
        }

        return Ok(result.Universities
            .Select(UniversidadResumenResponse.FromDomain)
            .ToArray());
    }
}
