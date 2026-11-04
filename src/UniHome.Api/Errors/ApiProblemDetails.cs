using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace UniHome.Api.Errors;

/// <summary>
/// Construye ProblemDetails homogéneos sin incluir stack traces, datos personales
/// ni detalles internos. El traceId permite correlacionar con los logs.
/// </summary>
internal static class ApiProblemDetails
{
    private const string TraceIdKey = "traceId";

    public static ProblemDetails Create(
        HttpContext context,
        int statusCode,
        string? title = null,
        string? detail = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title ?? GetDefaultTitle(statusCode),
            Type = GetTypeUri(statusCode)
        };

        if (!string.IsNullOrWhiteSpace(detail))
        {
            problem.Detail = detail;
        }

        problem.Extensions[TraceIdKey] = GetTraceId(context);

        return problem;
    }

    public static ValidationProblemDetails CreateValidation(
        HttpContext context,
        ModelStateDictionary modelState)
    {
        var problem = new ValidationProblemDetails(modelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "La solicitud contiene datos inválidos.",
            Type = GetTypeUri(StatusCodes.Status400BadRequest),
            Detail = "Revise los campos indicados en errors."
        };

        problem.Extensions[TraceIdKey] = GetTraceId(context);

        return problem;
    }

    public static string GetTraceId(HttpContext context) =>
        Activity.Current?.Id ?? context.TraceIdentifier;

    private static string GetTypeUri(int statusCode) =>
        $"https://httpstatuses.io/{statusCode}";

    private static string GetDefaultTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Solicitud inválida.",
        StatusCodes.Status401Unauthorized => "No autorizado.",
        StatusCodes.Status403Forbidden => "Prohibido.",
        StatusCodes.Status404NotFound => "Recurso no encontrado.",
        StatusCodes.Status405MethodNotAllowed => "Método no permitido.",
        StatusCodes.Status409Conflict => "Conflicto.",
        StatusCodes.Status413PayloadTooLarge => "Solicitud demasiado grande.",
        StatusCodes.Status415UnsupportedMediaType => "Tipo de contenido no soportado.",
        StatusCodes.Status422UnprocessableEntity => "Regla de negocio no satisfecha.",
        StatusCodes.Status429TooManyRequests => "Demasiadas solicitudes.",
        StatusCodes.Status500InternalServerError => "Error interno del servidor.",
        StatusCodes.Status503ServiceUnavailable => "Servicio no disponible.",
        _ => "Error en la solicitud."
    };
}
