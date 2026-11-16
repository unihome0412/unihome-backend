namespace UniHome.Infrastructure.Redis;

/// <summary>
/// Modo de falla ante indisponibilidad de Redis.
/// Según ADR-006, la decisión final entre FailClosed y FailOpen
/// está pendiente de ratificación y consenso del equipo (Jesui y Bridget).
/// </summary>
public enum RedisFailureMode
{
    /// <summary>
    /// Rechaza la solicitud con HTTP 503 si Redis falla o no responde.
    /// Recomendado para máxima seguridad contra ataques de fuerza bruta en endpoints críticos.
    /// </summary>
    FailClosed,

    /// <summary>
    /// Permite la solicitud con registro de advertencia si Redis falla.
    /// No degrada a contadores locales independientes para evitar falsear límites globales entre réplicas.
    /// </summary>
    FailOpen
}

/// <summary>
/// Opciones de configuración para la integración con Upstash Redis mediante su API REST sobre HTTPS.
/// Nota técnica sobre el protocolo:
/// La implementación se comunica con Upstash exclusivamente mediante solicitudes HTTP POST seguras (REST sobre HTTPS).
/// No utiliza el protocolo binario RESP de Redis sobre TCP. Si se proporciona una cadena con esquema rediss://,
/// el cliente extrae el host y el token para consultar el endpoint REST HTTPS del clúster de Upstash.
/// Las credenciales deben proporcionarse exclusivamente mediante variables de entorno o almacén de secretos.
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public const int DefaultCommandTimeoutMs = 2000;
    public const int MinCommandTimeoutMs = 250;
    public const int MaxCommandTimeoutMs = 10000;
    public const int DefaultReadinessTimeoutMs = 800;
    public const int MinReadinessTimeoutMs = 250;
    public const int MaxReadinessTimeoutMs = 2000;
    public const int DefaultMaxAttempts = 2;
    public const int MaxAttemptsLimit = 3;
    public const int DefaultRetryDelayMs = 100;
    public const int MaxRetryDelayMs = 1000;

    /// <summary>
    /// Indica si el rate limiting distribuido con Redis está activo.
    /// Es estrictamente obligatorio en entornos distintos de Development para garantizar
    /// límites compartidos entre múltiples réplicas en Azure Container Apps.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Cadena de conexión estándar (esquema rediss) de Upstash configurada mediante variables de entorno.
    /// Se transforma internamente a endpoint REST sobre HTTPS.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// URL del endpoint REST de Upstash (https://xxx.upstash.io) o UPSTASH_REDIS_REST_URL.
    /// </summary>
    public string? RestUrl { get; set; }

    /// <summary>
    /// Token de autenticación REST de Upstash o UPSTASH_REDIS_REST_TOKEN.
    /// </summary>
    public string? RestToken { get; set; }

    /// <summary>
    /// Timeout por intento en milisegundos para comandos de Redis.
    /// 250 ms fue demasiado bajo para el primer request HTTPS a Upstash y produjo
    /// TaskCanceledException con 503 intermitentes. El valor acotado de 2000 ms
    /// tolera la conexión TLS en frío sin dejar colgada la petición del cliente.
    /// </summary>
    public int CommandTimeoutMs { get; set; } = DefaultCommandTimeoutMs;

    /// <summary>
    /// Número máximo de intentos por comando ante fallos transitorios
    /// (timeout, error de conexión o HTTP 408/425/429/5xx). Acotado a 3.
    /// Los errores de autenticación o de contrato no se reintentan.
    /// </summary>
    public int MaxAttempts { get; set; } = DefaultMaxAttempts;

    /// <summary>
    /// Espera fija entre reintentos transitorios, en milisegundos. Acotada a 1000.
    /// </summary>
    public int RetryDelayMs { get; set; } = DefaultRetryDelayMs;

    /// <summary>
    /// Timeout en milisegundos para el PING de /health/ready. Se mantiene por
    /// debajo del timeout de la sonda de Azure y no reintenta para no alargar el probe.
    /// </summary>
    public int ReadinessTimeoutMs { get; set; } = DefaultReadinessTimeoutMs;

    /// <summary>
    /// Política ante caídas o timeouts de Redis.
    /// El valor predeterminado seguro es FailClosed (seguridad primero).
    /// Su selección definitiva para producción requiere ratificación de Jesui y Bridget.
    /// </summary>
    public RedisFailureMode FailureMode { get; set; } = RedisFailureMode.FailClosed;
}
