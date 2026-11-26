using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UniHome.Api.Caching;

/// <summary>
/// Política de output cache para los GET públicos del Bloque 2. A
/// diferencia de <c>OutputCachePolicyBuilder.Expire(TimeSpan)</c> (que fija
/// el TTL en el momento de registrar la política, antes de que la
/// configuración de pruebas termine de aplicarse — ver Memoria.md), esta
/// política resuelve <see cref="OutputCacheOptions"/> en cada petición vía
/// <see cref="IOptionsMonitor{TOptions}"/>, así que siempre usa el TTL
/// configurado realmente vigente.
/// </summary>
internal sealed class PublicOutputCachePolicy : IOutputCachePolicy
{
    private readonly string _tag;
    private readonly bool _requireAnonymous;

    public PublicOutputCachePolicy(string tag, bool requireAnonymous)
    {
        _tag = tag;
        _requireAnonymous = requireAnonymous;
    }

    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var allowed = httpContext.Request.Method == HttpMethods.Get ||
                      httpContext.Request.Method == HttpMethods.Head;

        if (_requireAnonymous &&
            httpContext.Request.Headers.ContainsKey(Microsoft.Net.Http.Headers.HeaderNames.Authorization))
        {
            allowed = false;
        }

        context.EnableOutputCaching = allowed;
        context.AllowCacheLookup = allowed;
        context.AllowCacheStorage = allowed;
        context.AllowLocking = true;

        if (allowed)
        {
            context.Tags.Add(_tag);
            // Explícito a propósito: dos peticiones a la misma ruta con un
            // query string distinto (p. ej. "?buscar=a" vs "?buscar=b") nunca
            // deben compartir entrada de caché.
            context.CacheVaryByRules.QueryKeys = "*";

            var options = httpContext.RequestServices
                .GetRequiredService<IOptionsMonitor<OutputCacheOptions>>()
                .CurrentValue;
            context.ResponseExpirationTimeSpan = TimeSpan.FromSeconds(options.PublicTtlSeconds);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask CacheResponseAsync(OutputCacheContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
