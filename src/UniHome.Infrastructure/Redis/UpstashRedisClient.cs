using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UniHome.Infrastructure.Redis;

/// <summary>
/// Cliente mínimo de comandos Redis contra la API REST de Upstash (HTTPS,
/// nunca el protocolo binario RESP sobre TCP), con la misma política de
/// timeout/reintento que <see cref="UpstashRedisDistributedRateLimiter"/>.
/// Usado por los adaptadores nuevos del Bloque 2 (caché de salida y denylist
/// de sesión) para no duplicar la resolución de credenciales ni la lógica de
/// reintento en cada uno. El rate limiter no se tocó para no arriesgar una
/// regresión en un control de seguridad ya probado; este cliente es código
/// nuevo, sin consumidores previos.
/// </summary>
internal sealed class UpstashRedisClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ILogger _logger;
    private readonly string _resolvedRestUrl;
    private readonly string _resolvedToken;
    private readonly int _commandTimeoutMs;
    private readonly int _readinessTimeoutMs;
    private readonly int _maxAttempts;
    private readonly int _retryDelayMs;

    public UpstashRedisClient(
        IOptions<RedisOptions> options,
        ILogger logger,
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var (restUrl, token) = ResolveCredentials(options.Value);
        _resolvedRestUrl = restUrl;
        _resolvedToken = token;

        _commandTimeoutMs = Math.Clamp(
            options.Value.CommandTimeoutMs > 0 ? options.Value.CommandTimeoutMs : RedisOptions.DefaultCommandTimeoutMs,
            RedisOptions.MinCommandTimeoutMs,
            RedisOptions.MaxCommandTimeoutMs);
        _readinessTimeoutMs = Math.Clamp(
            options.Value.ReadinessTimeoutMs > 0 ? options.Value.ReadinessTimeoutMs : RedisOptions.DefaultReadinessTimeoutMs,
            RedisOptions.MinReadinessTimeoutMs,
            RedisOptions.MaxReadinessTimeoutMs);
        _maxAttempts = Math.Clamp(options.Value.MaxAttempts, 1, RedisOptions.MaxAttemptsLimit);
        _retryDelayMs = Math.Clamp(options.Value.RetryDelayMs, 0, RedisOptions.MaxRetryDelayMs);

        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            _ownsHttpClient = true;
        }
    }

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(_resolvedRestUrl) && !string.IsNullOrWhiteSpace(_resolvedToken);

    /// <summary>SET key value EX seconds (siempre con expiración; nunca una clave sin TTL).</summary>
    public async Task<bool> SetAsync(
        string key,
        string value,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(ttl.TotalSeconds));
        var result = await ExecuteAsync(
            ["SET", key, value, "EX", seconds.ToString()],
            cancellationToken).ConfigureAwait(false);
        return result.Succeeded;
    }

    /// <summary>EXISTS key.</summary>
    public async Task<bool?> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(["EXISTS", key], cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded || result.Result is null)
        {
            return null;
        }

        return result.Result.Value.ValueKind == JsonValueKind.Number &&
               result.Result.Value.GetInt64() > 0;
    }

    /// <summary>GET key; null si no existe o si Redis no respondió.</summary>
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(["GET", key], cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded || result.Result is null ||
            result.Result.Value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return result.Result.Value.GetString();
    }

    /// <summary>DEL key.</summary>
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken) =>
        ExecuteSucceedsAsync(["DEL", key], cancellationToken);

    /// <summary>SADD key member, seguido de EXPIRE key seconds (el índice de un
    /// tag no debe sobrevivir indefinidamente si se deja de escribir en él).</summary>
    public async Task<bool> SetAddAsync(
        string key,
        string member,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        var added = await ExecuteSucceedsAsync(["SADD", key, member], cancellationToken)
            .ConfigureAwait(false);

        if (!added)
        {
            return false;
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(ttl.TotalSeconds));
        return await ExecuteSucceedsAsync(["EXPIRE", key, seconds.ToString()], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>SMEMBERS key.</summary>
    public async Task<string[]> SetMembersAsync(string key, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(["SMEMBERS", key], cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded || result.Result is null ||
            result.Result.Value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return result.Result.Value
            .EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.String)
            .Select(element => element.GetString()!)
            .ToArray();
    }

    /// <summary>PING de readiness, sin reintentos, con el timeout acotado de sonda.</summary>
    public async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        if (!HasCredentials)
        {
            return false;
        }

        var result = await ExecuteAsync(
            ["PING"],
            cancellationToken,
            timeoutMsOverride: _readinessTimeoutMs,
            maxAttemptsOverride: 1).ConfigureAwait(false);

        return result.Succeeded &&
               result.Result?.ValueKind == JsonValueKind.String &&
               string.Equals(result.Result.Value.GetString(), "PONG", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> ExecuteSucceedsAsync(object[] payload, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(payload, cancellationToken).ConfigureAwait(false);
        return result.Succeeded;
    }

    private async Task<CommandResult> ExecuteAsync(
        object[] payload,
        CancellationToken cancellationToken,
        int? timeoutMsOverride = null,
        int? maxAttemptsOverride = null)
    {
        if (!HasCredentials)
        {
            _logger.LogWarning("Redis no está configurado; no se ejecutó el comando.");
            return CommandResult.Failed();
        }

        var timeoutMs = timeoutMsOverride ?? _commandTimeoutMs;
        var maxAttempts = maxAttemptsOverride ?? _maxAttempts;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);

            try
            {
                using var request = CreateRequest(payload);
                using var response = await _httpClient
                    .SendAsync(request, timeout.Token)
                    .ConfigureAwait(false);

                if (IsTransient(response.StatusCode) && attempt < maxAttempts)
                {
                    await DelayBeforeRetryAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error HTTP {StatusCode} al comunicar con Upstash Redis.", response.StatusCode);
                    return CommandResult.Failed();
                }

                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var document = await JsonDocument
                    .ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (document.RootElement.TryGetProperty("error", out var error))
                {
                    _logger.LogError("Upstash Redis respondió con error: {Error}", error.GetString());
                    return CommandResult.Failed();
                }

                if (document.RootElement.TryGetProperty("result", out var result))
                {
                    return CommandResult.Ok(result.Clone());
                }

                return CommandResult.Failed();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Redis no respondió en {TimeoutMs} ms (intento {Attempt}/{Attempts}).",
                    timeoutMs, attempt, maxAttempts);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "Fallo de conexión con Redis (intento {Attempt}/{Attempts}).",
                    attempt, maxAttempts);
            }

            if (attempt < maxAttempts)
            {
                await DelayBeforeRetryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return CommandResult.Failed();
    }

    private HttpRequestMessage CreateRequest(object[] payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _resolvedRestUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _resolvedToken);
        return request;
    }

    private Task DelayBeforeRetryAsync(CancellationToken cancellationToken) =>
        _retryDelayMs == 0 ? Task.CompletedTask : Task.Delay(_retryDelayMs, cancellationToken);

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
        || (int)statusCode == 425;

    private static (string RestUrl, string Token) ResolveCredentials(RedisOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.RestUrl) && !string.IsNullOrWhiteSpace(options.RestToken))
        {
            return (options.RestUrl.TrimEnd('/'), options.RestToken.Trim());
        }

        var connString = options.ConnectionString ??
                         Environment.GetEnvironmentVariable("REDIS_CONNECTIONSTRING") ??
                         Environment.GetEnvironmentVariable("Redis__ConnectionString");

        if (!string.IsNullOrWhiteSpace(connString) &&
            Uri.TryCreate(connString, UriKind.Absolute, out var uri))
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

                return ($"https://{uri.Host}", token);
            }

            if (uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return (uri.ToString().TrimEnd('/'), options.RestToken ?? string.Empty);
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

    private readonly record struct CommandResult(bool Succeeded, JsonElement? Result)
    {
        public static CommandResult Ok(JsonElement result) => new(true, result);
        public static CommandResult Failed() => new(false, null);
    }
}
