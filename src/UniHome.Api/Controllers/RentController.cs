using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Rentals;
using UniHome.Application.Rentals.Configure;
using UniHome.Application.Rentals.Payments;
using UniHome.Application.Rentals.Split;
using UniHome.Domain.Rentals;

namespace UniHome.Api.Controllers;

/// <summary>
/// EP-M10 (HU-050/051/052/053/054/055/056/057/058/060/061): configuración,
/// división, pagos informativos, vista del inquilino y resumen mensual sobre
/// la renta de una unidad por periodo. UniHome no procesa pagos ni
/// interviene en transacciones económicas entre las partes (HU-090).
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
    private readonly GetMyRentShareService _myRentShare;
    private readonly GetMyPaymentHistoryService _myPaymentHistory;
    private readonly GetMyRentSummaryService _mySummary;

    public RentController(
        ConfigureRentPeriodService configure,
        SplitRentPeriodService split,
        RegisterRentPaymentService registerPayment,
        ListRentPaymentsService listPayments,
        GetMyRentShareService myRentShare,
        GetMyPaymentHistoryService myPaymentHistory,
        GetMyRentSummaryService mySummary)
    {
        _configure = configure;
        _split = split;
        _registerPayment = registerPayment;
        _listPayments = listPayments;
        _myRentShare = myRentShare;
        _myPaymentHistory = myPaymentHistory;
        _mySummary = mySummary;
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

    /// <summary>EP-M10-05 (HU-057): la propia cuota del inquilino en el periodo, con lo pagado y su saldo.</summary>
    [HttpGet("unidades/{unitId:guid}/renta/{periodo}/mi-renta")]
    [Authorize]
    [ProducesResponseType(typeof(MiRentaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> MyRentShare(
        Guid unitId,
        string periodo,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _myRentShare
            .GetAsync(accountId, unitId, periodo, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(MiRentaResponse.FromView(value)),
            notFoundDetail: "La renta de este periodo no está configurada o no tienes una cuota asignada.",
            forbiddenDetail: "Debes tener un vínculo activo con la unidad para consultar tu renta.",
            conflictDetail: "No se pudo completar la operación.",
            unprocessableDetail: "La división de este periodo está pendiente.");
    }

    /// <summary>EP-M10-06 (HU-058): historial propio de pagos informativos en la unidad, a través de todos los periodos.</summary>
    [HttpGet("unidades/{unitId:guid}/renta/mis-pagos")]
    [Authorize]
    [ProducesResponseType(typeof(MisPagosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> MyPaymentHistory(
        Guid unitId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _myPaymentHistory
            .GetAsync(accountId, unitId, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(MisPagosResponse.FromItems(value)),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo completar la operación.");
    }

    /// <summary>
    /// EP-M10-07 (HU-060/061): resumen mensual de renta de la cuenta para un
    /// periodo, como titular (por cada unidad propia) y como inquilino (por
    /// cada unidad con vínculo activo).
    /// </summary>
    [HttpGet("mi-resumen-renta/{periodo}")]
    [Authorize]
    [ProducesResponseType(typeof(MiResumenRentaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> MySummary(
        string periodo,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _mySummary.GetAsync(accountId, periodo, cancellationToken).ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(MiResumenRentaResponse.FromView(value)),
            notFoundDetail: "No se pudo completar la operación.",
            forbiddenDetail: "La operación no está permitida.",
            conflictDetail: "No se pudo completar la operación.");
    }
}
