using Microsoft.AspNetCore.Diagnostics;
using UniHome.Application.Identity.Exceptions;

namespace UniHome.Api.Errors;

/// <summary>
/// Manejo centralizado de errores no controlados. Responde 500 con ProblemDetails
/// genérico y registra solo el tipo de excepción y el traceId (sin PII ni payloads).
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted ||
            (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested))
        {
            return false;
        }

        var traceId = ApiProblemDetails.GetTraceId(httpContext);
        var statusCode = exception switch
        {
            BadHttpRequestException badRequest => badRequest.StatusCode,
            StorageUnavailableException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                "Excepción no controlada. Tipo={ExceptionType} TraceId={TraceId}",
                exception.GetType().FullName,
                traceId);
        }
        else
        {
            _logger.LogWarning(
                "Solicitud rechazada por el servidor. Tipo={ExceptionType} TraceId={TraceId}",
                exception.GetType().FullName,
                traceId);
        }

        httpContext.Response.StatusCode = statusCode;

        var problem = ApiProblemDetails.Create(
            httpContext,
            statusCode,
            detail: statusCode >= StatusCodes.Status500InternalServerError
                ? "Se produjo un error inesperado. Use el traceId para reportarlo."
                : null);

        await ApiProblemDetailsWriter
            .WriteAsync(httpContext, problem, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
