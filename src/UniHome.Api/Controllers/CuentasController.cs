using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UniHome.Api.Contracts.Cuentas;
using UniHome.Api.Errors;
using UniHome.Application.Identity.DeleteAccount;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/cuentas")]
public sealed class CuentasController : ControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;

    private readonly DeleteAccountService _deleteAccount;

    public CuentasController(DeleteAccountService deleteAccount)
    {
        _deleteAccount = deleteAccount;
    }

    /// <summary>
    /// EP-M01-09 (HU-096): elimina (borrado lógico) la cuenta del titular
    /// autenticado y revoca todas sus sesiones activas en todos los
    /// dispositivos. La validación de vínculos activos (409, M05/M09) y la
    /// política de retención/anonimización (ADR-005) quedan pendientes.
    /// </summary>
    [HttpDelete("mi-cuenta")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteMyAccount(
        [FromBody] EliminarCuentaRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        var accountClaim = User.FindFirst("sub")?.Value;

        if (!Guid.TryParseExact(accountClaim, "N", out var accountId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var command = new DeleteAccountCommand(
            accountId,
            request.Contrasena,
            request.ConfirmacionPerdidaInformacion);

        var result = await _deleteAccount
            .DeleteAsync(command, cancellationToken)
            .ConfigureAwait(false);

        return result.Status switch
        {
            DeleteAccountStatus.Deleted => NoContentWithoutCache(),
            DeleteAccountStatus.ConfirmationMissing => CreateValidationProblem(
                "confirmacionPerdidaInformacion",
                "Debe confirmar que entiende que perderá su información."),
            DeleteAccountStatus.IncorrectPassword => CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La contraseña no es correcta."),
            _ => CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.")
        };
    }

    private NoContentResult NoContentWithoutCache()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        return NoContent();
    }

    private BadRequestObjectResult CreateValidationProblem(string field, string message)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(field, message);

        return new BadRequestObjectResult(ApiProblemDetails.CreateValidation(HttpContext, modelState))
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
