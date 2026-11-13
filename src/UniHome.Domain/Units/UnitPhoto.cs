namespace UniHome.Domain.Units;

/// <summary>Metadatos de una fotografía; el binario vive en el proveedor externo.</summary>
public sealed record UnitPhoto(
    string StorageKey,
    string ContentType,
    long SizeBytes,
    string Sha256,
    int Order,
    bool IsPrimary,
    DateTimeOffset CreatedAt);
