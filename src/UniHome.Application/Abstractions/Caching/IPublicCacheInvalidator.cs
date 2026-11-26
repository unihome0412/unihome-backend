namespace UniHome.Application.Abstractions.Caching;

/// <summary>
/// Invalidación del output cache de GET públicos por tag (ver
/// <see cref="PublicCacheTags"/>). El TTL corto ya limita cuánto puede durar
/// una respuesta obsoleta; esto la corta de inmediato cuando la operación lo
/// exige (aprobar/rechazar un expediente, crear/editar/cambiar el estado de
/// una universidad).
/// </summary>
public interface IPublicCacheInvalidator
{
    Task EvictAsync(string tag, CancellationToken cancellationToken = default);
}
