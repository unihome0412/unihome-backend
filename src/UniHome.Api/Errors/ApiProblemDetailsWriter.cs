using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace UniHome.Api.Errors;

internal static class ApiProblemDetailsWriter
{
    public const string ContentType = "application/problem+json; charset=utf-8";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(
        HttpContext context,
        ProblemDetails problem,
        CancellationToken cancellationToken) =>
        context.Response.WriteAsJsonAsync(problem, SerializerOptions, ContentType, cancellationToken);
}
