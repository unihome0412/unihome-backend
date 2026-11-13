using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Units.Abstractions;

namespace UniHome.Infrastructure.Storage;

/// <summary>
/// Implementación fail-closed mientras DEC-04 no autorice un proveedor de blobs.
/// No simula almacenamiento ni escribe archivos en el repositorio o MongoDB.
/// </summary>
internal sealed class UnconfiguredUnitPhotoStorage : IUnitPhotoStorage
{
    public Task<StoredUnitPhoto> SaveAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default) =>
        throw new StorageUnavailableException();

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public string GetReadUrl(string storageKey) =>
        throw new StorageUnavailableException();
}
