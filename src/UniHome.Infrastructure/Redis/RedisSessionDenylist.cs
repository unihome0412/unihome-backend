using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniHome.Application.Identity.Abstractions;

namespace UniHome.Infrastructure.Redis;

/// <summary>
/// Denylist de <c>sid</c> respaldada por Upstash Redis (API REST). A
/// diferencia del rate limiting (FailClosed por defecto, protege contra
/// fuerza bruta), esta comprobación corre en el camino crítico de **toda**
/// petición autenticada: si Redis no responde, se falla abierto (se permite
/// la petición) y se registra una advertencia. BE-SEC-008 (el estado de la
/// cuenta se revisa contra MongoDB en cada petición) sigue siendo la puerta
/// de seguridad primaria; esta denylist es una revocación inmediata
/// complementaria, no la única defensa — una caída de Redis nunca debe
/// convertirse en una caída total de la API autenticada.
/// </summary>
public sealed class RedisSessionDenylist : ISessionDenylist, IDisposable
{
    private const string KeyPrefix = "denylist:sid:";

    private readonly UpstashRedisClient _client;
    private readonly ILogger<RedisSessionDenylist> _logger;

    public RedisSessionDenylist(
        IOptions<RedisOptions> options,
        ILogger<RedisSessionDenylist> logger,
        HttpClient? httpClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = new UpstashRedisClient(options, logger, httpClient);
    }

    public async Task DenyAsync(Guid sessionId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return;
        }

        var succeeded = await _client
            .SetAsync(BuildKey(sessionId), "1", ttl, cancellationToken)
            .ConfigureAwait(false);

        if (!succeeded)
        {
            _logger.LogWarning(
                "No se pudo denylistear el sid {SessionId} en Redis; BE-SEC-008 sigue cubriendo cuentas " +
                "suspendidas/eliminadas, pero la revocación inmediata de este token de acceso no quedó aplicada.",
                sessionId);
        }
    }

    public async Task<bool> IsDeniedAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return false;
        }

        var exists = await _client
            .ExistsAsync(BuildKey(sessionId), cancellationToken)
            .ConfigureAwait(false);

        // null = Redis no respondió: falla abierto (ver resumen de la clase).
        return exists ?? false;
    }

    private static string BuildKey(Guid sessionId) => KeyPrefix + sessionId.ToString("N");

    public void Dispose() => _client.Dispose();
}
