namespace UniHome.Application.Abstractions.RateLimiting;

/// <summary>
/// Resultado de evaluar una solicitud contra el rate limiter distribuido.
/// </summary>
public sealed record RateLimitResult(
    bool IsAllowed,
    long CurrentCount,
    TimeSpan RetryAfter,
    bool IsServiceUnavailable = false,
    string? ErrorMessage = null)
{
    public static RateLimitResult Allowed(long currentCount, TimeSpan retryAfter) =>
        new(true, currentCount, retryAfter);

    public static RateLimitResult Exceeded(long currentCount, TimeSpan retryAfter) =>
        new(false, currentCount, retryAfter);

    public static RateLimitResult ServiceUnavailable(string errorMessage) =>
        new(false, 0, TimeSpan.Zero, IsServiceUnavailable: true, ErrorMessage: errorMessage);
}

/// <summary>
/// Resultado de la comprobación de disponibilidad del almacén de rate limiting.
/// No contiene credenciales, URLs ni mensajes de excepción.
/// </summary>
public sealed record RateLimiterReadiness(bool IsReady, string? Detail = null)
{
    public static RateLimiterReadiness Ready() => new(true);

    public static RateLimiterReadiness Unavailable(string? detail = null) =>
        new(false, detail);
}

/// <summary>
/// Puerto de disponibilidad del almacén distribuido que respalda el rate limiting.
/// readiness lo consume para no declarar lista la API si Redis es obligatorio y falla.
/// </summary>
public interface IRateLimiterReadinessProbe
{
    Task<RateLimiterReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Contrato para el rate limiting distribuido compartido entre réplicas.
/// </summary>
public interface IDistributedRateLimiter : IRateLimiterReadinessProbe
{
    /// <summary>
    /// Evalúa e incrementa atómicamente el contador para la clave especificada.
    /// </summary>
    Task<RateLimitResult> CheckAndIncrementAsync(
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}
