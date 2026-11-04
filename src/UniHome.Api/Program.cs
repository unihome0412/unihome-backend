using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using UniHome.Api.Errors;
using UniHome.Api.HealthChecks;
using UniHome.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Composición de la aplicación: Api orquesta; Application/Domain/Infrastructure
// se registran detrás de sus puertos.
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            ApiProblemDetails.GetTraceId(context.HttpContext);
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = ApiProblemDetails.CreateValidation(context.HttpContext, context.ModelState);

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { ApiProblemDetailsWriter.ContentType }
        };
    };
});

builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Proceso vivo."),
        tags: [HealthCheckTags.Live])
    .AddCheck<StorageHealthCheck>(
        "storage",
        tags: [HealthCheckTags.Ready]);

builder.Services.AddOpenApi("v1");

var app = builder.Build();

// Errores homogéneos: excepciones no controladas y códigos sin cuerpo.
app.UseExceptionHandler();
app.UseStatusCodePages(async statusCodeContext =>
{
    var http = statusCodeContext.HttpContext;

    if (http.Response.HasStarted)
    {
        return;
    }

    var problem = ApiProblemDetails.Create(http, http.Response.StatusCode);
    await ApiProblemDetailsWriter
        .WriteAsync(http, problem, http.RequestAborted)
        .ConfigureAwait(false);
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains(HealthCheckTags.Live),
    ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready),
    ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();

app.MapControllers();

app.Run();

public partial class Program;
