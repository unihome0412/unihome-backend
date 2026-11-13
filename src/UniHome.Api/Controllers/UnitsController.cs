using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Units;
using UniHome.Application.Units.CreateUnit;
using UniHome.Application.Units.Photos;
using UniHome.Application.Units.Preview;
using UniHome.Application.Units.Services;
using UniHome.Application.Units.ZoneRules;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class UnitsController : AuthenticatedControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;
    private const long MaxPhotoRequestBytes = 52 * 1024 * 1024;

    private readonly CreateUnitService _createUnit;
    private readonly AddUnitPhotosService _addPhotos;
    private readonly ConfigureUnitServicesService _configureServices;
    private readonly ConfigureUnitZoneRulesService _configureZoneRules;
    private readonly GetUnitPreviewService _getPreview;

    public UnitsController(
        CreateUnitService createUnit,
        AddUnitPhotosService addPhotos,
        ConfigureUnitServicesService configureServices,
        ConfigureUnitZoneRulesService configureZoneRules,
        GetUnitPreviewService getPreview)
    {
        _createUnit = createUnit;
        _addPhotos = addPhotos;
        _configureServices = configureServices;
        _configureZoneRules = configureZoneRules;
        _getPreview = getPreview;
    }

    /// <summary>
    /// EP-M05-02 (HU-031/HU-078/HU-079): crea una unidad en Borrador dentro de
    /// una propiedad del titular.
    /// </summary>
    [HttpPost("propiedades/{propertyId:guid}/unidades")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(UnidadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        Guid propertyId,
        [FromBody] CrearUnidadRequest request,
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
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _createUnit
            .CreateAsync(
                accountId,
                propertyId,
                new CreateUnitCommand(
                    request.TipoUnidad,
                    request.Ocupacion,
                    request.Lugares,
                    request.PrecioMensualPorLugarMxn,
                    request.TamanoM2,
                    request.ParaQuien,
                    request.Bano,
                    request.Mobiliario,
                    request.Muebles,
                    request.PlantillaBase),
                cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => StatusCode(StatusCodes.Status201Created, UnidadResponse.FromDomain(value)),
            notFoundDetail: "La propiedad no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "La unidad entra en conflicto con otra operación.");
    }

    /// <summary>
    /// EP-M05-03 (HU-032): agrega hasta 10 fotos JPG/PNG a una unidad propia.
    /// El binario se delega al almacenamiento externo de DEC-04.
    /// </summary>
    [HttpPost("unidades/{unitId:guid}/fotos")]
    [Authorize]
    [RequestSizeLimit(MaxPhotoRequestBytes)]
    [ProducesResponseType(typeof(UnidadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AddPhotos(
        Guid unitId,
        [FromForm] AgregarFotosRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxPhotoRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        if (request.Fotos.Any(file => file.Length > UnitPhotoValidator.MaxBytesPerPhoto))
        {
            return CreateValidationResult(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["fotos"] =
                    [
                        $"Cada fotografía debe pesar como máximo " +
                        $"{UnitPhotoValidator.MaxBytesPerPhoto / (1024 * 1024)} MB."
                    ]
                });
        }

        var uploads = new List<UnitPhotoUpload>(request.Fotos.Count);

        foreach (var file in request.Fotos)
        {
            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            uploads.Add(new UnitPhotoUpload(file.FileName, file.ContentType, memory.ToArray()));
        }

        var result = await _addPhotos
            .AddAsync(accountId, unitId, uploads, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(UnidadResponse.FromDomain(value)),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "Otra carga modificó las fotos de la unidad. Vuelva a intentarlo.");
    }

    /// <summary>EP-M05-04 (HU-081): reemplaza los servicios de una unidad propia.</summary>
    [HttpPut("unidades/{unitId:guid}/servicios")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(UnidadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ConfigureServices(
        Guid unitId,
        [FromBody] ServiciosRequest request,
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
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _configureServices
            .ConfigureAsync(
                accountId,
                unitId,
                new ConfigureUnitServicesCommand(
                    request.Servicios?
                        .Select(service => new ServiceChargeInput(
                            service.Nombre,
                            service.Modalidad,
                            service.MontoMensualMxn))
                        .ToArray()),
                cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(UnidadResponse.FromDomain(value)),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "La unidad fue modificada por otra operación.");
    }

    /// <summary>EP-M05-05 (HU-082/HU-083): zona y reglas de una unidad propia.</summary>
    [HttpPut("unidades/{unitId:guid}/zona-reglas")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(UnidadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ConfigureZoneRules(
        Guid unitId,
        [FromBody] ZonaReglasRequest request,
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
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _configureZoneRules
            .ConfigureAsync(
                accountId,
                unitId,
                new ConfigureUnitZoneRulesCommand(
                    request.InformacionZona,
                    request.ReglasConvivencia),
                cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(UnidadResponse.FromDomain(value)),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "La unidad fue modificada por otra operación.");
    }

    /// <summary>
    /// EP-M05-06 (HU-080): tarjeta de vista previa de una unidad propia. No
    /// declara publicada una unidad en Borrador.
    /// </summary>
    [HttpGet("unidades/{unitId:guid}/vista-previa")]
    [Authorize]
    [ProducesResponseType(typeof(UnidadVistaPreviaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Preview(
        Guid unitId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _getPreview
            .GetAsync(accountId, unitId, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(UnidadVistaPreviaResponse.FromView(value)),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo construir la vista previa.");
    }
}
