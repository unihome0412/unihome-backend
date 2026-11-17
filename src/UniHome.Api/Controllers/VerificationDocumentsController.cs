using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Configuration;
using UniHome.Application.Verification.Admin;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/expedientes/documentos")]
[Authorize(Policy = AdminAccessOptions.PolicyName)]
public sealed class VerificationDocumentsController : AuthenticatedControllerBase
{
    private readonly ReadVerificationDocumentService _read;

    public VerificationDocumentsController(ReadVerificationDocumentService read)
    {
        _read = read;
    }

    /// <summary>
    /// Consume el enlace firmado. Nunca es público: exige privilegio
    /// administrativo, valida expiración/firma y audita el acceso antes de
    /// descifrar la respuesta.
    /// </summary>
    [HttpGet("{token}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Read(
        string token,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var administratorId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _read
            .ReadAsync(administratorId, token, cancellationToken)
            .ConfigureAwait(false);

        if (result.Status != Application.Common.OperationStatus.Success)
        {
            return ToActionResult(
                result,
                _ => StatusCode(StatusCodes.Status500InternalServerError),
                notFoundDetail: "El documento no existe o el enlace venció.",
                forbiddenDetail: "La cuenta no tiene privilegio administrativo.",
                conflictDetail: "No se pudo abrir el documento.");
        }

        var document = result.Value!;
        Response.Headers.CacheControl = "no-store";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'";
        Response.Headers.XContentTypeOptions = "nosniff";

        return document.IsThumbnail
            ? File(document.Content, document.ContentType)
            : File(
                document.Content,
                document.ContentType,
                fileDownloadName: document.FileName,
                enableRangeProcessing: false);
    }
}
