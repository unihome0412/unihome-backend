using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UniHome.Api.Configuration;
using UniHome.Api.Contracts.Universities;
using UniHome.Api.Errors;
using UniHome.Application.Universities.ChangeUniversityStatus;
using UniHome.Application.Universities.CreateUniversity;
using UniHome.Application.Universities.ListUniversities;
using UniHome.Application.Universities.UpdateUniversity;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/admin/universidades")]
[Authorize(Policy = AdminAccessOptions.PolicyName)]
public sealed class AdminUniversitiesController : ControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;

    private readonly CreateUniversityService _createUniversity;
    private readonly ListUniversitiesService _listUniversities;
    private readonly UpdateUniversityService _updateUniversity;
    private readonly ChangeUniversityStatusService _changeUniversityStatus;

    public AdminUniversitiesController(
        CreateUniversityService createUniversity,
        ListUniversitiesService listUniversities,
        UpdateUniversityService updateUniversity,
        ChangeUniversityStatusService changeUniversityStatus)
    {
        _createUniversity = createUniversity;
        _listUniversities = listUniversities;
        _updateUniversity = updateUniversity;
        _changeUniversityStatus = changeUniversityStatus;
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

    /// <summary>
    /// EP-M03-03 (HU-077): lista paginada del catálogo administrativo. Incluye
    /// activas e inactivas; el total viaja en <c>X-Total-Count</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<UniversidadResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ListUniversitiesService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _listUniversities
            .ListAsync(page, pageSize, cancellationToken)
            .ConfigureAwait(false);

        if (result.Page is null)
        {
            return CreateValidationResult(result.ValidationErrors);
        }

        Response.Headers["X-Total-Count"] = result.Page.Total.ToString(
            CultureInfo.InvariantCulture);
        Response.Headers["X-Page"] = page.ToString(CultureInfo.InvariantCulture);
        Response.Headers["X-Page-Size"] = pageSize.ToString(CultureInfo.InvariantCulture);

        return Ok(result.Page.Items
            .Select(UniversidadResponse.FromDomain)
            .ToArray());
    }

    /// <summary>
    /// EP-M03-04 (HU-077): edita nombre y campus. Un duplicado normalizado
    /// responde 409 y una edición concurrente perdedora también.
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(UniversidadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] EditarUniversidadRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        var result = await _updateUniversity
            .UpdateAsync(id, request.NombreYCampus, cancellationToken)
            .ConfigureAwait(false);

        return result.Status switch
        {
            UpdateUniversityStatus.Updated => Ok(
                UniversidadResponse.FromDomain(result.University!)),
            UpdateUniversityStatus.NotFound => CreateProblemResult(
                StatusCodes.Status404NotFound,
                "La universidad no existe."),
            UpdateUniversityStatus.Duplicate => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Ya existe una universidad con ese nombre y campus."),
            UpdateUniversityStatus.Conflict => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "La universidad fue modificada por otra operación. Vuelva a consultarla."),
            _ => CreateValidationResult(result.ValidationErrors)
        };
    }

    /// <summary>
    /// EP-M03-05 (HU-077): activa o desactiva sin borrar. La búsqueda pública
    /// solo muestra activas; el catálogo administrativo conserva ambas.
    /// </summary>
    [HttpPut("{id:guid}/estado")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(UniversidadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] CambiarEstadoUniversidadRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        var result = await _changeUniversityStatus
            .ChangeAsync(id, request.Activa, cancellationToken)
            .ConfigureAwait(false);

        return result.Status switch
        {
            ChangeUniversityStatus.Updated => Ok(
                UniversidadResponse.FromDomain(result.University!)),
            _ => CreateProblemResult(
                StatusCodes.Status404NotFound,
                "La universidad no existe.")
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
