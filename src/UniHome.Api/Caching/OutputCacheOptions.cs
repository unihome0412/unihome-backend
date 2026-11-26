namespace UniHome.Api.Caching;

/// <summary>
/// Vigencia del output cache de GET públicos (Bloque 2 de la auditoría).
/// Configurable para poder ajustarla en producción sin recompilar, y para
/// que las pruebas de integración puedan usar un TTL corto y determinista
/// en vez de esperar el valor real.
/// </summary>
public sealed class OutputCacheOptions
{
    public const string SectionName = "OutputCache";

    public const int DefaultPublicTtlSeconds = 20;
    public const int MinPublicTtlSeconds = 1;
    public const int MaxPublicTtlSeconds = 300;

    public int PublicTtlSeconds { get; set; } = DefaultPublicTtlSeconds;

    public bool IsValid() =>
        PublicTtlSeconds is >= MinPublicTtlSeconds and <= MaxPublicTtlSeconds;
}
