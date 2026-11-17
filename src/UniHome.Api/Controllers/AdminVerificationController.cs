using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Configuration;
using UniHome.Api.Contracts.Verification;
using UniHome.Application.Common;
using UniHome.Application.Verification.Admin;
using UniHome.Domain.Verification;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/admin/expedientes")]
[Authorize(Policy = AdminAccessOptions.PolicyName)]
public sealed class AdminVerificationController : AuthenticatedControllerBase
{
    private readonly ListExpedientesService _list;
    private readonly GetExpedienteDetailService _detail;
    private readonly IssueDocumentAccessUrlService _documentUrl;

    public AdminVerificationController(
        ListExpedientesService list,
        GetExpedienteDetailService detail,
        IssueDocumentAccessUrlService documentUrl)
    {
        _list = list;
        _detail = detail;
        _documentUrl = documentUrl;
    }

    /// <summary>
    /// EP-M06-04 (HU-023): bandeja de primeras publicaciones. Ordena primero los
    /// sospechosos y publica el total y el contador pendiente en cabeceras.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<ExpedienteResumenResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List(
        [FromQuery] EstadoExpediente? estado,
        [FromQuery] bool prioridadSospechosos = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ListExpedientesService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _list
            .ListAsync(estado, prioridadSospechosos, page, pageSize, cancellationToken)
            .ConfigureAwait(false);

        if (result.Status != OperationStatus.Success)
        {
            return ToActionResult(
                result,
                _ => StatusCode(StatusCodes.Status500InternalServerError),
                notFoundDetail: "No hay expedientes.",
                forbiddenDetail: "La cuenta no tiene privilegio administrativo.",
                conflictDetail: "No se pudo consultar la bandeja.");
        }

        var view = result.Value!;
        Response.Headers["X-Total-Count"] = view.Total.ToString(CultureInfo.InvariantCulture);
        Response.Headers["X-Page"] = page.ToString(CultureInfo.InvariantCulture);
        Response.Headers["X-Page-Size"] = pageSize.ToString(CultureInfo.InvariantCulture);
        Response.Headers["X-Pending-Review-Count"] =
            view.PendingReviewCount.ToString(CultureInfo.InvariantCulture);

        return Ok(view.Items.Select(ExpedienteResumenResponse.FromView).ToArray());
    }

    /// <summary>
    /// EP-M06-05 (HU-016/HU-018/HU-020/HU-021/HU-022): detalle con datos de
    /// contacto, OCR, indicador de edición y enlaces temporales a documentos.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ExpedienteDetalleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Detail(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var administratorId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _detail
            .GetAsync(administratorId, id, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(ExpedienteDetalleResponse.FromView(value)),
            notFoundDetail: "El expediente no existe.",
            forbiddenDetail: "La cuenta no tiene privilegio administrativo.",
            conflictDetail: "No se pudo consultar el expediente.");
    }

    /// <summary>
    /// EP-M06-06 (HU-016/HU-088): entrega un enlace firmado de corta vida. El
    /// enlace sigue exigiendo una sesión administrativa y queda auditado.
    /// </summary>
    [HttpGet("{id:guid}/documentos/{tipo}")]
    [ProducesResponseType(typeof(DocumentoUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DocumentUrl(
        Guid id,
        TipoDocumentoVerificacion tipo,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var administratorId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _documentUrl
            .IssueAsync(administratorId, id, tipo, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(DocumentoUrlResponse.FromView(value)),
            notFoundDetail: "El documento no existe.",
            forbiddenDetail: "La cuenta no tiene privilegio administrativo.",
            conflictDetail: "No se pudo emitir el enlace.");
    }
}
