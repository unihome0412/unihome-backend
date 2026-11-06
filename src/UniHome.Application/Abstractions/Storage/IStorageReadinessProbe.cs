namespace UniHome.Application.Abstractions.Storage;

/// <summary>
/// Puerto de Application que comprueba la disponibilidad del almacenamiento.
/// Infrastructure proporciona el adaptador concreto; Api lo consume para readiness.
/// </summary>
public interface IStorageReadinessProbe
{
    Task<StorageReadiness> CheckAsync(CancellationToken cancellationToken = default);
}
