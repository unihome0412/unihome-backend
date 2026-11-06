namespace UniHome.Application.Abstractions.Storage;

/// <summary>
/// Resultado de una comprobación de disponibilidad del almacenamiento.
/// No contiene cadenas de conexión ni datos de configuración sensibles.
/// </summary>
public sealed record StorageReadiness(StorageReadinessStatus Status, string? Detail = null)
{
    public bool IsReady => Status == StorageReadinessStatus.Ready;

    public static StorageReadiness Ready() =>
        new(StorageReadinessStatus.Ready);

    public static StorageReadiness NotConfigured() =>
        new(StorageReadinessStatus.NotConfigured);

    public static StorageReadiness Unavailable(string? detail = null) =>
        new(StorageReadinessStatus.Unavailable, detail);
}
