using UniHome.Application.Identity.Exceptions;

namespace UniHome.Application.Units.Abstractions;

public sealed record UnitPhotoContent(
    Stream Content,
    string ContentType,
    long? ContentLength);

/// <summary>
/// Puerto de almacenamiento de fotografías. La implementación de producción es
/// Cloudinary con entrega autenticada; el binario nunca se guarda en MongoDB.
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

    /// <summary>
    /// Descarga el contenido desde el proveedor para el endpoint proxy. Devuelve
    /// null si el recurso ya no existe.
    /// </summary>
    Task<UnitPhotoContent?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default) =>
        throw new StorageUnavailableException();

    /// <summary>
    /// URL de lectura autorizada de la API (no la URL interna de Cloudinary).
    /// </summary>
    string GetReadUrl(string storageKey);
}

public sealed record StoredUnitPhoto(
    string StorageKey,
    string ContentType,
    long SizeBytes,
    string Sha256);
