using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using UniHome.Api.Configuration;
using UniHome.Api.Contracts.Auth;
using UniHome.Api.Errors;
using UniHome.Application.Identity.Login;
using UniHome.Application.Identity.Logout;
using UniHome.Application.Identity.RefreshSessions;
using UniHome.Application.Identity.RegisterAccount;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private const long MaxAuthRequestBytes = 8 * 1024;

    private readonly RegisterAccountService _registerAccount;
    private readonly LoginService _login;
    private readonly RefreshSessionService _refreshSession;
    private readonly LogoutService _logout;

    public AuthController(
        RegisterAccountService registerAccount,
        LoginService login,
        RefreshSessionService refreshSession,
        LogoutService logout)
    {
        _registerAccount = registerAccount;
        _login = login;
        _refreshSession = refreshSession;
        _logout = logout;
    }

    /// <summary>
    /// EP-M01-01: registra una cuenta única sin exigir la elección de rol.
    /// </summary>
    [HttpPost("registro")]
    [AllowAnonymous]
    [RequestSizeLimit(MaxAuthRequestBytes)]
    [ProducesResponseType(typeof(RegistroResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Register(
        [FromBody] RegistroRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxAuthRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
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
            return CreateProblemResult(
                StatusCodes.Status409Conflict,
                "No fue posible completar el registro con los datos proporcionados.");
        }

        if (!result.Succeeded)
        {
            return CreateValidationResult(result.ValidationErrors);
        }

        var response = new RegistroResponse(
            result.AccountId!.Value,
            result.Email!,
            CorreoConfirmado: false,
            RequiereConfirmacionCorreo: true,
            CorreoConfirmacionEnviado: result.VerificationEmailSent);

        SetNoStore();
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// EP-M01-04: inicio de sesión común para cualquier cuenta, sin selección de rol.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.Login)]
    [RequestSizeLimit(MaxAuthRequestBytes)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxAuthRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        var result = await _login
            .LoginAsync(new LoginCommand(request.Correo, request.Contrasena), cancellationToken)
            .ConfigureAwait(false);

        return result.Status switch
        {
            LoginStatus.Success => CreateTokenResponse(result),
            LoginStatus.ValidationFailed => CreateValidationResult(result.ValidationErrors),
            LoginStatus.EmailNotConfirmed => CreateProblemResult(
                StatusCodes.Status403Forbidden,
                "Confirma tu correo electrónico para iniciar sesión."),
            LoginStatus.Suspended => CreateProblemResult(
                StatusCodes.Status403Forbidden,
                "La cuenta está suspendida."),
            _ => CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "Revisa tu correo y contraseña")
        };
    }

    /// <summary>
    /// EP-M01-08: renueva la sesión con rotación del refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.Refresh)]
    [RequestSizeLimit(MaxAuthRequestBytes)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxAuthRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        var result = await _refreshSession
            .RefreshAsync(new RefreshSessionCommand(request.RefreshToken), cancellationToken)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            SetNoStore();
            return Ok(new LoginResponse(
                result.AccessToken!,
                result.RefreshToken!,
                result.ExpiresAt!.Value));
        }

        if (result.Status == RefreshSessionStatus.ValidationFailed)
        {
            return CreateValidationResult(result.ValidationErrors);
        }

        return CreateProblemResult(
            StatusCodes.Status401Unauthorized,
            "La sesión no es válida o expiró.");
    }

    /// <summary>
    /// EP-M01-05: revoca únicamente la sesión del dispositivo actual.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [RequestSizeLimit(MaxAuthRequestBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var accountClaim = User.FindFirst("sub")?.Value;
        var sessionClaim = User.FindFirst("sid")?.Value;

        if (!Guid.TryParseExact(accountClaim, "N", out var accountId) ||
            !Guid.TryParseExact(sessionClaim, "N", out var sessionId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var revoked = await _logout
            .LogoutAsync(accountId, sessionId, cancellationToken)
            .ConfigureAwait(false);

        if (!revoked)
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        SetNoStore();
        return NoContent();
    }

    private ObjectResult CreateTokenResponse(LoginResult result)
    {
        SetNoStore();

        return Ok(new LoginResponse(
            result.AccessToken!,
            result.RefreshToken!,
            result.ExpiresAt!.Value));
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

    private void SetNoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }
}
