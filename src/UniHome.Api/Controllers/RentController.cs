using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Rentals;
using UniHome.Application.Rentals.Configure;
using UniHome.Application.Rentals.Payments;
using UniHome.Application.Rentals.Split;
using UniHome.Domain.Rentals;

namespace UniHome.Api.Controllers;

/// <summary>
/// EP-M10 (HU-050/051/052/053/054/055/056): configuración, división y pagos
/// informativos de la renta de una unidad por periodo. UniHome no procesa
/// pagos ni interviene en transacciones económicas entre las partes (HU-090).
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class RentController : AuthenticatedControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;

    private readonly ConfigureRentPeriodService _configure;
    private readonly SplitRentPeriodService _split;
    private readonly RegisterRentPaymentService _registerPayment;
    private readonly ListRentPaymentsService _listPayments;

    public RentController(
        ConfigureRentPeriodService configure,
        SplitRentPeriodService split,
        RegisterRentPaymentService registerPayment,
        ListRentPaymentsService listPayments)
    {
        _configure = configure;
        _split = split;
        _registerPayment = registerPayment;
        _listPayments = listPayments;
    }

    /// <summary>EP-M10-01 (HU-050/051/052): define renta total, conceptos y día de corte.</summary>
    [HttpPut("unidades/{unitId:guid}/renta/{periodo}")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(RentaPeriodoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Configure(
        Guid unitId,
        string periodo,
        [FromBody] ConfigurarRentaRequest request,
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

        var concepts = request.Conceptos?
            .Select(input => new RentConcept(input.Nombre, input.EsFijo))
            .ToArray();

        var result = await _configure
            .ConfigureAsync(
                accountId,
                unitId,
                periodo,
                request.RentaTotalMxn,
                request.DiaCorte,
                concepts,
                cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(RentaPeriodoResponse.FromDomain(value)),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo completar la operación.");
    }

    /// <summary>EP-M10-02 (HU-053/HU-054): divide la renta del periodo entre los inquilinos activos.</summary>
    [HttpPut("unidades/{unitId:guid}/renta/{periodo}/division")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(RentaPeriodoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Split(
        Guid unitId,
        string periodo,
        [FromBody] DividirRentaRequest request,
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

        var shares = request.Cuotas?
            .Select(input => new TenantShare(input.InquilinoAccountId, input.MontoMxn))
            .ToArray();

        var result = await _split
            .SplitAsync(accountId, unitId, periodo, request.Metodo, shares, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(RentaPeriodoResponse.FromDomain(value)),
            notFoundDetail: "La renta de este periodo no está configurada.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo completar la operación.");
    }

    /// <summary>EP-M10-03 (HU-055): registra el pago informativo recibido de un inquilino.</summary>
    [HttpPost("unidades/{unitId:guid}/renta/{periodo}/pagos/{tenantAccountId:guid}")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(PagoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RegisterPayment(
        Guid unitId,
        string periodo,
        Guid tenantAccountId,
        [FromBody] RegistrarPagoRequest request,
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

        var result = await _registerPayment
            .RegisterAsync(
                accountId,
                unitId,
                periodo,
                tenantAccountId,
                request.MontoMxn,
                request.FechaRecepcion,
                cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => StatusCode(StatusCodes.Status201Created, PagoResponse.FromDomain(value)),
            notFoundDetail: "La renta de este periodo no está configurada.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo completar la operación.");
    }

    /// <summary>EP-M10-04 (HU-056): estado de pago de cada inquilino del periodo frente a su cuota.</summary>
    [HttpGet("unidades/{unitId:guid}/renta/{periodo}/pagos")]
    [Authorize]
    [ProducesResponseType(typeof(EstadoPagosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListPayments(
        Guid unitId,
        string periodo,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _listPayments
            .ListAsync(accountId, unitId, periodo, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(EstadoPagosResponse.FromSummary(value)),
            notFoundDetail: "La renta de este periodo no está configurada.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo completar la operación.",
            unprocessableDetail: "La división de este periodo está pendiente; configúrala antes de consultar pagos.");
    }
}
