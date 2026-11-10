using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using UniHome.Api.Configuration;
using UniHome.Api.Errors;
using UniHome.Api.HealthChecks;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.ConfirmEmail;
using UniHome.Application.Identity.DeleteAccount;
using UniHome.Application.Identity.Login;
using UniHome.Application.Identity.Logout;
using UniHome.Application.Identity.RefreshSessions;
using UniHome.Application.Identity.RegisterAccount;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure;
using UniHome.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Fuera de Development el HMAC debe venir de un almacén de secretos: una clave
// efímera por proceso invalidaría códigos al reiniciar o cambiar de instancia.
var hmacSecret = builder.Configuration[
    $"{VerificationCodeOptions.SectionName}:HmacSecret"];

if (!builder.Environment.IsDevelopment() &&
    (string.IsNullOrWhiteSpace(hmacSecret) || hmacSecret.Length < VerificationCodeOptions.MinSecretLength))
{
    throw new InvalidOperationException(
        $"Configure {VerificationCodeOptions.SectionName}:HmacSecret con al menos " +
        $"{VerificationCodeOptions.MinSecretLength} caracteres fuera de Development.");
}

// Composición de la aplicación: Api orquesta; Application/Domain/Infrastructure
// se registran detrás de sus puertos.
builder.Services.AddInfrastructure(builder.Configuration);

// La clave JWT se valida después de que todas las fuentes de configuración
// (incluidas las de pruebas) hayan sido aplicadas. En Development se genera una
// clave efímera para no guardar secretos en appsettings.
builder.Services
    .AddOptions<JwtOptions>()
    .Configure<IHostEnvironment>((options, environment) =>
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"Configure {JwtOptions.SectionName}:SigningKey con al menos " +
                    $"{JwtOptions.MinSigningKeyLength} caracteres fuera de Development.");
            }

            options.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            options.EphemeralSigningKey = true;
        }
        else if (options.SigningKey.Length < JwtOptions.MinSigningKeyLength)
        {
            throw new InvalidOperationException(
                $"La clave {JwtOptions.SectionName}:SigningKey debe tener al menos " +
                $"{JwtOptions.MinSigningKeyLength} caracteres.");
        }

        if (options.AccessTokenMinutes <= 0 || options.RefreshTokenDays <= 0)
        {
            throw new InvalidOperationException(
                "Las vigencias JWT deben ser mayores que cero.");
        }
    })
    .Validate(
        options => options.HasSecureSigningKey,
        $"La clave {JwtOptions.SectionName}:SigningKey no es segura.")
    .ValidateOnStart();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // El contrato de M01 usa los nombres de género y no acepta valores numéricos.
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false));
    });

builder.Services.AddScoped<RegisterAccountService>();
builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<RefreshSessionService>();
builder.Services.AddScoped<LogoutService>();
builder.Services.AddScoped<ConfirmEmailService>();
builder.Services.AddScoped<ResendConfirmationService>();
builder.Services.AddScoped<DeleteAccountService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
    {
        var jwtOptions = jwtOptionsAccessor.Value;

        options.MapInboundClaims = false;
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey!)),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidTypes = ["JWT"],
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub"
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var subject = context.Principal?.FindFirst("sub")?.Value;
                var session = context.Principal?.FindFirst("sid")?.Value;

                if (!Guid.TryParseExact(subject, "N", out var accountId) ||
                    !Guid.TryParseExact(session, "N", out _))
                {
                    context.Fail("El token no contiene un sujeto o una sesión válida.");
                    return;
                }

                // BE-SEC-008: una cuenta suspendida o eliminada no puede seguir
                // usando un access token emitido antes del cambio de estado.
                var accounts = context.HttpContext.RequestServices
                    .GetRequiredService<IAccountRepository>();

                var account = await accounts
                    .FindByIdAsync(accountId, context.HttpContext.RequestAborted)
                    .ConfigureAwait(false);

                if (account is null ||
                    account.Status != AccountStatus.Active ||
                    !account.IsEmailConfirmed)
                {
                    context.Fail("La cuenta no está habilitada.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.Configure<AuthRateLimitOptions>(
    builder.Configuration.GetSection(AuthRateLimitOptions.SectionName));

builder.Services.Configure<ProxyOptions>(
    builder.Configuration.GetSection(ProxyOptions.SectionName));

builder.Services.Configure<ApiOptions>(
    builder.Configuration.GetSection(ApiOptions.SectionName));

// Azure Container Apps termina TLS en su ingress y reenvía HTTP; estos
// encabezados solo se aplican si Proxy:TrustForwardedHeaders=true. ForwardLimit
// en 1: no se confía en una cadena de varios valores enviada por el cliente.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    // El valor predeterminado solo confía en el salto inmediato si viene de
    // loopback. El ingress de Container Apps no tiene una IP conocida y fija;
    // activar Proxy:TrustForwardedHeaders ya es la declaración explícita de que
    // la topología solo permite llegar al contenedor a través de ese ingress.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math
                .Ceiling(retryAfter.TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);
        }

        // No se registra la IP ni el correo intentado.
        var problem = ApiProblemDetails.Create(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            detail: "Demasiados intentos. Intenta de nuevo más tarde.");

        await ApiProblemDetailsWriter
            .WriteAsync(context.HttpContext, problem, cancellationToken)
            .ConfigureAwait(false);
    };

    options.AddPolicy(
        AuthRateLimitPolicies.Login,
        httpContext =>
        {
            var limits = httpContext.RequestServices
                .GetRequiredService<IOptions<AuthRateLimitOptions>>()
                .Value;

            return RateLimitPartition.GetFixedWindowLimiter(
                GetClientPartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, limits.LoginPermitLimit),
                    Window = TimeSpan.FromSeconds(Math.Max(1, limits.LoginWindowSeconds)),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });

    options.AddPolicy(
        AuthRateLimitPolicies.Refresh,
        httpContext =>
        {
            var limits = httpContext.RequestServices
                .GetRequiredService<IOptions<AuthRateLimitOptions>>()
                .Value;

            return RateLimitPartition.GetFixedWindowLimiter(
                GetClientPartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, limits.RefreshPermitLimit),
                    Window = TimeSpan.FromSeconds(Math.Max(1, limits.RefreshWindowSeconds)),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });

    options.AddPolicy(
        AuthRateLimitPolicies.ConfirmEmail,
        httpContext =>
        {
            var limits = httpContext.RequestServices
                .GetRequiredService<IOptions<AuthRateLimitOptions>>()
                .Value;

            return RateLimitPartition.GetFixedWindowLimiter(
                GetClientPartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, limits.ConfirmEmailPermitLimit),
                    Window = TimeSpan.FromSeconds(Math.Max(1, limits.ConfirmEmailWindowSeconds)),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });

    options.AddPolicy(
        AuthRateLimitPolicies.Resend,
        httpContext =>
        {
            var limits = httpContext.RequestServices
                .GetRequiredService<IOptions<AuthRateLimitOptions>>()
                .Value;

            return RateLimitPartition.GetFixedWindowLimiter(
                GetClientPartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, limits.ResendPermitLimit),
                    Window = TimeSpan.FromSeconds(Math.Max(1, limits.ResendWindowSeconds)),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });
});

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

builder.Services.AddOpenApi("v1", options =>
{
    // Esquema nativo para que el contrato documente el Bearer de logout sin
    // instalar Scalar ni Swagger UI.
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Token de acceso JWT emitido por POST /api/v1/auth/login."
        };

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var requiresAuthentication = metadata.OfType<IAuthorizeData>().Any() &&
            !metadata.OfType<IAllowAnonymous>().Any();

        if (!requiresAuthentication)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });

        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Cabeceras de endurecimiento aplicadas a todas las respuestas, incluidas las
// generadas por el manejador de errores (OnStarting se ejecuta al iniciar el
// envío). La CSP estricta es para respuestas de API; Swagger UI necesita una
// política que permita sus recursos propios e inline.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        var path = context.Request.Path;
        headers["Content-Security-Policy"] = path.StartsWithSegments("/swagger") || path == "/"
            ? "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'"
            : "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

        if (path.StartsWithSegments("/api/v1/auth") ||
            path.StartsWithSegments("/api/v1/cuentas"))
        {
            headers.CacheControl = "no-store";
        }

        return Task.CompletedTask;
    });

    await next();
});

var jwtSettings = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

if (jwtSettings.EphemeralSigningKey)
{
    app.Logger.LogWarning(
        "JWT:SigningKey no está configurada. Se usa una clave efímera de Development; " +
        "los tokens emitidos dejarán de ser válidos al reiniciar el proceso.");
}

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

var proxyOptions = app.Services.GetRequiredService<IOptions<ProxyOptions>>().Value;

if (proxyOptions.TrustForwardedHeaders)
{
    // Debe ir antes de HSTS, HTTPS redirection, rate limiter y autenticación:
    // todos ellos deben ver el esquema/IP ya reescritos, no los del salto HTTP
    // interno entre el ingress de Container Apps y este proceso.
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

var apiOptions = app.Services.GetRequiredService<IOptions<ApiOptions>>().Value;

if (apiOptions.EnableSwagger)
{
    // Documento OpenAPI nativo de .NET 10; Swagger UI solo lo renderiza, no lo
    // genera (sin Swashbuckle.AspNetCore completo). El esquema Bearer y los
    // requisitos de seguridad por operación ya están en el documento (ver
    // AddOpenApi arriba), así que el botón "Authorize" de Swagger UI los toma
    // de ahí sin configuración adicional.
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "UniHome API v1");
        options.RoutePrefix = "swagger";
    });
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
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

static string GetClientPartitionKey(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

public partial class Program;
