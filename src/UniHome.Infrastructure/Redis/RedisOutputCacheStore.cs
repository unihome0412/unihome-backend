using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UniHome.Infrastructure.Redis;

/// <summary>
/// Caché de salida (Output Cache) respaldada por Upstash Redis, para que el
/// caché se comparta entre réplicas de Azure Container Apps en vez de vivir
/// en memoria de una sola instancia. Solo se usa en políticas explícitas de
/// endpoints públicos (ver <c>OutputCachePolicies</c> en <c>UniHome.Api</c>);
/// esta clase no decide qué se cachea, solo almacena lo que el middleware le
/// pide.
///
/// El índice de tags (para <see cref="EvictByTagAsync"/>) es un SET de Redis
/// por tag con los ids de entrada asociados; invalidar un tag borra cada
/// entrada y luego el propio índice. No es atómico entre pasos (Upstash REST
/// no expone MULTI/EXEC aquí), pero el peor caso es servir una entrada ya
/// invalidada por, como máximo, el tiempo de una sola llamada HTTP a Redis —
/// aceptable para un TTL corto de caché pública, no para un control de
/// seguridad.
/// </summary>
public sealed class RedisOutputCacheStore : IOutputCacheStore, IDisposable
{
    private const string EntryPrefix = "outputcache:entry:";
    private const string TagPrefix = "outputcache:tag:";

    // Upper bound generoso para el índice de tags: si una entrada ya expiró
    // pero su id sigue en el set del tag, EvictByTagAsync simplemente borra
    // una clave que ya no existe (no-op), nunca revive algo vencido.
    private static readonly TimeSpan TagIndexTtl = TimeSpan.FromHours(1);

    private readonly UpstashRedisClient _client;
    private readonly ILogger<RedisOutputCacheStore> _logger;

    public RedisOutputCacheStore(
        IOptions<RedisOptions> options,
        ILogger<RedisOutputCacheStore> logger,
        HttpClient? httpClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = new UpstashRedisClient(options, logger, httpClient);
    }

    public async ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var tagKey = TagPrefix + tag;
        var entryIds = await _client.SetMembersAsync(tagKey, cancellationToken).ConfigureAwait(false);

        foreach (var entryId in entryIds)
        {
            await _client.DeleteAsync(EntryPrefix + entryId, cancellationToken).ConfigureAwait(false);
        }

        await _client.DeleteAsync(tagKey, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var value = await _client.GetAsync(EntryPrefix + key, cancellationToken).ConfigureAwait(false);

        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            _logger.LogWarning("Entrada de output cache corrupta para la clave {Key}; se trata como ausente.", key);
            return null;
        }
    }

    public async ValueTask SetAsync(
        string key,
        byte[] value,
        string[]? tags,
        TimeSpan validFor,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        await _client
            .SetAsync(EntryPrefix + key, Convert.ToBase64String(value), validFor, cancellationToken)
            .ConfigureAwait(false);

        if (tags is null)
        {
            return;
        }

        foreach (var tag in tags)
        {
            await _client
                .SetAddAsync(TagPrefix + tag, key, TagIndexTtl, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public void Dispose() => _client.Dispose();
}
