using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UniHome.Api.Contracts.Auth;
using UniHome.Api.Errors;
using UniHome.Application.Identity.RegisterAccount;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private const long MaxRegistrationRequestBytes = 8 * 1024;

    private readonly RegisterAccountService _registerAccount;

    public AuthController(RegisterAccountService registerAccount)
    {
        _registerAccount = registerAccount;
    }

    /// <summary>
    /// EP-M01-01: registra una cuenta única sin exigir la elección de rol.
    /// </summary>
    [HttpPost("registro")]
    [AllowAnonymous]
    [RequestSizeLimit(8 * 1024)]
    [ProducesResponseType(typeof(RegistroResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Register(
        [FromBody] RegistroRequest request,
        CancellationToken cancellationToken)
    {
        // TestServer y algunos proxies no exponen IHttpMaxRequestBodySizeFeature;
        // la comprobación de Content-Length mantiene el límite verificable.
        if (Request.ContentLength is > MaxRegistrationRequestBytes)
        {
            var problem = ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status413PayloadTooLarge,
                detail: "La solicitud excede el tamaño permitido.");

            return new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status413PayloadTooLarge,
                ContentTypes = { ApiProblemDetailsWriter.ContentType }
            };
        }

        var command = new RegisterAccountCommand(
            request.NombreCompleto,
            request.Correo,
            request.FechaNacimiento,
            request.Genero,
            request.Contrasena,
            request.ConfirmarContrasena,
            request.AceptoAvisoPrivacidad,
            request.VersionAvisoPrivacidad);

        var result = await _registerAccount
            .RegisterAsync(command, cancellationToken)
            .ConfigureAwait(false);

        if (result.EmailAlreadyRegistered)
        {
            // DEC-09 sigue pendiente: se conserva el 409 documentado por la spec,
            // pero el detalle no confirma qué dato provocó el conflicto.
            var problem = ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status409Conflict,
                detail: "No fue posible completar el registro con los datos proporcionados.");

            return new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status409Conflict,
                ContentTypes = { ApiProblemDetailsWriter.ContentType }
            };
        }

        if (!result.Succeeded)
        {
            var modelState = new ModelStateDictionary();

            foreach (var (field, messages) in result.ValidationErrors)
            {
                foreach (var message in messages)
                {
                    modelState.AddModelError(field, message);
                }
            }

            var problem = ApiProblemDetails.CreateValidation(HttpContext, modelState);

            return new BadRequestObjectResult(problem)
            {
                ContentTypes = { ApiProblemDetailsWriter.ContentType }
            };
        }

        var response = new RegistroResponse(
            result.AccountId!.Value,
            result.Email!,
            CorreoConfirmado: false,
            RequiereConfirmacionCorreo: true,
            CorreoConfirmacionEnviado: result.VerificationEmailSent);

        // La respuesta contiene un correo del propio solicitante.
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";

        return StatusCode(StatusCodes.Status201Created, response);
    }
}
