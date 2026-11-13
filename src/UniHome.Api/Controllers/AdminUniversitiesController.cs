using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UniHome.Api.Configuration;
using UniHome.Api.Contracts.Universities;
using UniHome.Api.Errors;
using UniHome.Application.Universities.CreateUniversity;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/admin/universidades")]
[Authorize(Policy = AdminAccessOptions.PolicyName)]
public sealed class AdminUniversitiesController : ControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;

    private readonly CreateUniversityService _createUniversity;

    public AdminUniversitiesController(CreateUniversityService createUniversity)
    {
        _createUniversity = createUniversity;
    }

    /// <summary>
    /// EP-M03-02 (HU-075): da de alta una universidad activa con nombre y campus.
    /// La ruta exige el privilegio administrativo revalidado en el servidor.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(UniversidadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        [FromBody] CrearUniversidadRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        var result = await _createUniversity
            .CreateAsync(
                new CreateUniversityCommand(request.NombreYCampus),
                cancellationToken)
            .ConfigureAwait(false);

        return result.Status switch
        {
            CreateUniversityStatus.Created => StatusCode(
                StatusCodes.Status201Created,
                UniversidadResponse.FromDomain(result.University!)),
            CreateUniversityStatus.Duplicate => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Ya existe una universidad con ese nombre y campus."),
            _ => CreateValidationResult(result.ValidationErrors)
        };
    }

    private BadRequestObjectResult CreateValidationResult(
        IReadOnlyDictionary<string, string[]> errors)
    {
        var modelState = new ModelStateDictionary();

        foreach (var (field, messages) in errors)
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

    private ObjectResult CreateProblemResult(int statusCode, string detail) =>
        new(ApiProblemDetails.Create(HttpContext, statusCode, detail: detail))
        {
            StatusCode = statusCode,
            ContentTypes = { ApiProblemDetailsWriter.ContentType }
        };
}
