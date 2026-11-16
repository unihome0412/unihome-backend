using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UniHome.Api.Configuration;
using UniHome.Api.Contracts.Verification;
using UniHome.Application.Common;
using UniHome.Application.Verification.Documents;
using UniHome.Domain.Verification;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/expedientes")]
public sealed class VerificationController : AuthenticatedControllerBase
{
    private readonly UploadVerificationDocumentService _upload;

    public VerificationController(UploadVerificationDocumentService upload)
    {
        _upload = upload;
    }

    /// <summary>
    /// EP-M06-01 (HU-015/HU-088): sube la INE en JPG, PNG o PDF. El contenido se
    /// valida por firma binaria, se cifra y solo se entrega por enlace temporal
    /// administrativo.
    /// </summary>
    [HttpPost("mi-expediente/ine")]
    [Authorize]
    [EnableRateLimiting(AuthRateLimitPolicies.DocumentUpload)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(VerificationDocumentValidator.MaxRequestBytes)]
    [ProducesResponseType(typeof(MiExpedienteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> UploadIne(
        [FromForm] SubirDocumentoRequest request,
        CancellationToken cancellationToken) =>
        UploadAsync(TipoDocumentoVerificacion.Ine, request, cancellationToken);

    /// <summary>
    /// EP-M06-02 (HU-017/HU-088): sube escritura, boleta predial o constancia de
    /// propiedad en JPG, PNG o PDF.
    /// </summary>
    [HttpPost("mi-expediente/comprobante")]
    [Authorize]
    [EnableRateLimiting(AuthRateLimitPolicies.DocumentUpload)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(VerificationDocumentValidator.MaxRequestBytes)]
    [ProducesResponseType(typeof(MiExpedienteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> UploadProof(
        [FromForm] SubirDocumentoRequest request,
        CancellationToken cancellationToken) =>
        UploadAsync(TipoDocumentoVerificacion.Comprobante, request, cancellationToken);

    private async Task<IActionResult> UploadAsync(
        TipoDocumentoVerificacion documentType,
        SubirDocumentoRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > VerificationDocumentValidator.MaxRequestBytes)
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

        if (request.Archivo is null || request.Archivo.Length == 0)
        {
            return CreateValidationResult(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["archivo"] = ["Selecciona un archivo JPG, PNG o PDF."]
                });
        }

        if (request.Archivo.Length > VerificationDocumentValidator.MaxBytesPerDocument)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                $"Cada documento debe pesar como máximo " +
                $"{VerificationDocumentValidator.MaxBytesPerDocument / (1024 * 1024)} MB.");
        }

        byte[] content;

        try
        {
            await using var stream = request.Archivo.OpenReadStream();
            using var memory = new MemoryStream(capacity: (int)request.Archivo.Length);
            var buffer = new byte[81920];
            long total = 0;

            while (true)
            {
                var read = await stream
                    .ReadAsync(buffer.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                total += read;

                if (total > VerificationDocumentValidator.MaxBytesPerDocument)
                {
                    return CreateProblemResult(
                        StatusCodes.Status413PayloadTooLarge,
                        "El documento excede el tamaño permitido.");
                }

                await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
            }

            content = memory.ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return CreateValidationResult(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["archivo"] = ["No se pudo leer el archivo enviado."]
                });
        }

        var fileName = string.IsNullOrWhiteSpace(request.NombreArchivoOriginal)
            ? request.Archivo.FileName
            : request.NombreArchivoOriginal;

        var result = await _upload
            .UploadAsync(
                accountId,
                documentType,
                new VerificationDocumentUpload(fileName, request.Archivo.ContentType, content),
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Status == OperationStatus.ValidationFailed)
        {
            var detail = result.ValidationErrors.TryGetValue("archivo", out var errors)
                ? errors.FirstOrDefault()
                : null;

            return CreateProblemResult(
                StatusCodes.Status422UnprocessableEntity,
                detail ?? "El archivo no tiene un formato permitido.");
        }

        return ToActionResult(
            result,
            value => Ok(MiExpedienteResponse.FromView(value.Expediente)),
            notFoundDetail: "Primero registra tu primera publicación para asociar los documentos.",
            forbiddenDetail: "Para verificar tu identidad necesitas una cuenta habilitada y ser mayor de 18 años.",
            conflictDetail: "El expediente está en revisión o aprobado y no admite cambios de evidencia.");
    }
}
