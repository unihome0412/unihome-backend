using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using UniHome.Api.Errors;

namespace UniHome.Api.HealthChecks;

internal static class HealthResponseWriter
{
    private const string ContentType = "application/json; charset=utf-8";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1)
                }),
            traceId = ApiProblemDetails.GetTraceId(context)
        };

        return context.Response.WriteAsJsonAsync(
            payload,
            SerializerOptions,
            ContentType,
            context.RequestAborted);
    }
}
