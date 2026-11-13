namespace UniHome.Application.Units.Abstractions;

public sealed record StoredUnitPhoto(
    string StorageKey,
    string ContentType,
    long SizeBytes,
    string Sha256);

/// <summary>
/// Puerto de almacenamiento de fotografías (DEC-04). El proveedor concreto queda
/// pendiente de aprobación; la API no guarda binarios en MongoDB ni en el repo.
/// </summary>
public interface IUnitPhotoStorage
{
    Task<StoredUnitPhoto> SaveAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    string GetReadUrl(string storageKey);
}
