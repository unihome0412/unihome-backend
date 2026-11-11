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
    /// Timeout estricto en milisegundos para comandos de Redis.
    /// El ADR-006 recomienda 250 ms para evitar el agotamiento de hilos en Kestrel.
    /// </summary>
    public int CommandTimeoutMs { get; set; } = 250;

    /// <summary>
    /// Política ante caídas o timeouts de Redis.
    /// El valor predeterminado seguro es FailClosed (seguridad primero).
    /// Su selección definitiva para producción requiere ratificación de Jesui y Bridget.
    /// </summary>
    public RedisFailureMode FailureMode { get; set; } = RedisFailureMode.FailClosed;
}
