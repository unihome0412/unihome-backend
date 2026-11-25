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
    /// dispositivos. Rechaza con 409 si tiene un vínculo de ocupación activo
    /// (M09) o una propiedad con inquilinos activos; con 403 si es una cuenta
    /// administrativa (ARQ-004). La política de retención/anonimización
    /// (ADR-005) queda pendiente.
    /// </summary>
    [HttpDelete("mi-cuenta")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
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
            DeleteAccountStatus.ActiveOccupancyLinks => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "La cuenta tiene un vínculo de ocupación activo; abandona la unidad antes de eliminar tu cuenta."),
            DeleteAccountStatus.PropertiesWithTenants => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Una de tus propiedades todavía tiene inquilinos activos."),
            DeleteAccountStatus.AdminCannotSelfDelete => CreateProblemResult(
                StatusCodes.Status403Forbidden,
                "Un administrador no puede eliminar su propia cuenta."),
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
