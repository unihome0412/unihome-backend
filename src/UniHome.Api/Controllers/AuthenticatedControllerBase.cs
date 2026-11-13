using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UniHome.Api.Errors;
using UniHome.Application.Common;

namespace UniHome.Api.Controllers;

/// <summary>
/// Utilidades compartidas por los controladores M05. El sujeto siempre se obtiene
/// del claim sub validado; nunca de un identificador enviado por el cliente.
/// </summary>
public abstract class AuthenticatedControllerBase : ControllerBase
{
    protected bool TryGetAccountId(out Guid accountId) =>
        Guid.TryParseExact(User.FindFirst("sub")?.Value, "N", out accountId);

    protected IActionResult ToActionResult<T>(
        OperationResult<T> result,
        Func<T, IActionResult> success,
        string notFoundDetail,
        string forbiddenDetail,
        string conflictDetail) =>
        result.Status switch
        {
            OperationStatus.Success => success(result.Value!),
            OperationStatus.ValidationFailed => CreateValidationResult(result.ValidationErrors),
            OperationStatus.NotFound => CreateProblemResult(
                StatusCodes.Status404NotFound,
                notFoundDetail),
            OperationStatus.Forbidden => CreateProblemResult(
                StatusCodes.Status403Forbidden,
                forbiddenDetail),
            OperationStatus.Conflict => CreateProblemResult(
                StatusCodes.Status409Conflict,
                conflictDetail),
            _ => CreateProblemResult(
                StatusCodes.Status500InternalServerError,
                "No se pudo completar la operación.")
        };

    protected BadRequestObjectResult CreateValidationResult(
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

    protected ObjectResult CreateProblemResult(int statusCode, string detail) =>
        new(ApiProblemDetails.Create(HttpContext, statusCode, detail: detail))
        {
            StatusCode = statusCode,
            ContentTypes = { ApiProblemDetailsWriter.ContentType }
        };
}
