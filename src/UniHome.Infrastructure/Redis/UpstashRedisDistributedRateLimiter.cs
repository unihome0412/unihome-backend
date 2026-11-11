using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniHome.Application.Abstractions.RateLimiting;

namespace UniHome.Infrastructure.Redis;

/// <summary>
/// Implementación de rate limiting distribuido respaldada por Upstash Redis mediante su API REST.
/// Utiliza un script Lua ejecutado de forma atómica con EVAL para garantizar que la verificación
/// y el incremento no sufran condiciones de carrera ni dependan de contadores locales por réplica.
/// </summary>
public sealed class UpstashRedisDistributedRateLimiter : IDistributedRateLimiter, IDisposable
{
    private const string RateLimitLuaScript =
        "local key = KEYS[1]\n" +
        "local window = tonumber(ARGV[1])\n" +
        "local limit = tonumber(ARGV[2])\n" +
        "local current = redis.call('INCR', key)\n" +
        "if current == 1 then\n" +
        "    redis.call('EXPIRE', key, window)\n" +
        "end\n" +
        "local ttl = redis.call('TTL', key)\n" +
        "if ttl == -1 then\n" +
        "    redis.call('EXPIRE', key, window)\n" +
        "    ttl = window\n" +
        "end\n" +
        "if current > limit then\n" +
        "    return {0, current, ttl}\n" +
        "else\n" +
        "    return {1, current, ttl}\n" +
        "end";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly IOptions<RedisOptions> _options;
    private readonly ILogger<UpstashRedisDistributedRateLimiter> _logger;
    private readonly string _resolvedRestUrl;
    private readonly string _resolvedToken;

    public UpstashRedisDistributedRateLimiter(
        IOptions<RedisOptions> options,
        ILogger<UpstashRedisDistributedRateLimiter> logger,
        HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var (restUrl, token) = ResolveCredentials(options.Value);
        _resolvedRestUrl = restUrl;
        _resolvedToken = token;

        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            var timeoutMs = options.Value.CommandTimeoutMs > 0 ? options.Value.CommandTimeoutMs : 250;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(timeoutMs)
            };
            _ownsHttpClient = true;
        }
    }

    public async Task<RateLimitResult> CheckAndIncrementAsync(
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

        if (string.IsNullOrWhiteSpace(_resolvedRestUrl) || string.IsNullOrWhiteSpace(_resolvedToken))
        {
            _logger.LogWarning(
                "Redis rate limiting está activo pero no se configuraron credenciales válidas. Aplicando modo de falla {FailureMode}.",
                _options.Value.FailureMode);

            return _options.Value.FailureMode == RedisFailureMode.FailClosed
                ? RateLimitResult.ServiceUnavailable("Credenciales de Redis no configuradas.")
                : RateLimitResult.Allowed(0, TimeSpan.Zero);
        }

        var windowSeconds = Math.Max(1, (int)Math.Ceiling(window.TotalSeconds));
        var payload = new object[]
        {
            "EVAL",
            RateLimitLuaScript,
            "1",
            partitionKey,
            windowSeconds.ToString(),
            permitLimit.ToString()
        };

        var requestJson = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, _resolvedRestUrl)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _resolvedToken);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Error HTTP {StatusCode} al comunicar con Upstash Redis para la clave {Key}.",
                    response.StatusCode,
                    partitionKey);

                return HandleFailure($"Error HTTP {response.StatusCode} desde Redis.");
            }

            var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("error", out var errorElement))
            {
                var redisError = errorElement.GetString() ?? "Unknown Redis Error";
                _logger.LogError("Upstash Redis respondió con error: {Error}", redisError);
                return HandleFailure($"Error retornado por Redis: {redisError}");
            }

            if (doc.RootElement.TryGetProperty("result", out var resultElement) &&
                resultElement.ValueKind == JsonValueKind.Array &&
                resultElement.GetArrayLength() >= 3)
            {
                var isAllowed = resultElement[0].GetInt32() == 1;
                var currentCount = resultElement[1].GetInt64();
                var ttlSeconds = resultElement[2].GetInt64();

                var remainingTtl = TimeSpan.FromSeconds(Math.Max(0, ttlSeconds));

                return isAllowed
                    ? RateLimitResult.Allowed(currentCount, remainingTtl)
                    : RateLimitResult.Exceeded(currentCount, remainingTtl);
            }

            _logger.LogError("Formato de respuesta inesperado de Upstash Redis.");
            return HandleFailure("Formato de respuesta no reconocido.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Excepción al ejecutar rate limit en Upstash Redis para la clave {Key}. Aplicando política {Mode}.",
                partitionKey,
                _options.Value.FailureMode);

            return HandleFailure(ex.Message);
        }
    }

    private RateLimitResult HandleFailure(string errorMessage)
    {
        if (_options.Value.FailureMode == RedisFailureMode.FailClosed)
        {
            return RateLimitResult.ServiceUnavailable(errorMessage);
        }

        // FailOpen: se permite la petición para continuidad del servicio,
        // pero NO se manipulan contadores locales en RAM para no crear particionamiento asimétrico.
        return RateLimitResult.Allowed(0, TimeSpan.Zero);
    }

    private static (string RestUrl, string Token) ResolveCredentials(RedisOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.RestUrl) && !string.IsNullOrWhiteSpace(options.RestToken))
        {
            return (options.RestUrl.TrimEnd('/'), options.RestToken.Trim());
        }

        var connString = options.ConnectionString ??
                         Environment.GetEnvironmentVariable("REDIS_CONNECTIONSTRING") ??
                         Environment.GetEnvironmentVariable("Redis__ConnectionString");

        if (!string.IsNullOrWhiteSpace(connString))
        {
            if (Uri.TryCreate(connString, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme.StartsWith("redis", StringComparison.OrdinalIgnoreCase))
                {
                    var userInfo = uri.UserInfo;
                    var token = string.Empty;
                    if (!string.IsNullOrEmpty(userInfo))
                    {
                        var parts = userInfo.Split(':');
                        token = parts.Length > 1 ? parts[1] : parts[0];
                    }

                    var hostUrl = $"https://{uri.Host}";
                    return (hostUrl, token);
                }

                if (uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    return (uri.ToString().TrimEnd('/'), options.RestToken ?? string.Empty);
                }
            }
        }

        var envRestUrl = Environment.GetEnvironmentVariable("UPSTASH_REDIS_REST_URL");
        var envRestToken = Environment.GetEnvironmentVariable("UPSTASH_REDIS_REST_TOKEN");

        if (!string.IsNullOrWhiteSpace(envRestUrl) && !string.IsNullOrWhiteSpace(envRestToken))
        {
            return (envRestUrl.TrimEnd('/'), envRestToken.Trim());
        }

        return (string.Empty, string.Empty);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
