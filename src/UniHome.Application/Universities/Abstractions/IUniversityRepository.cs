using UniHome.Domain.Universities;

namespace UniHome.Application.Universities.Abstractions;

/// <summary>
/// Puerto de persistencia del catálogo de universidades (M03). La unicidad del
/// nombre normalizado es responsabilidad del adaptador; no se sustituye por una
/// comprobación previa en Application.
/// </summary>
public interface IUniversityRepository
{
    Task AddAsync(University university, CancellationToken cancellationToken = default);

    Task<University?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Ubicación opcional del catálogo; se usa como sugerencia en M05.</summary>
    Task<GeoCoordinate?> GetLocationAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<UniversityPage> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<University>> SearchActiveAsync(
        string normalizedTerm,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza nombre y campus solo si el nombre normalizado esperado sigue
    /// vigente. Devuelve false si otra edición concurrente ganó o el documento
    /// ya no existe; un duplicado normalizado se traduce a excepción.
    /// </summary>
    Task<bool> UpdateNameAndCampusAsync(
        University university,
        string expectedNormalizedName,
        CancellationToken cancellationToken = default);

    /// <summary>Activa o desactiva sin borrar. Devuelve false si no existe.</summary>
    Task<bool> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);
}

public sealed record UniversityPage(IReadOnlyList<University> Items, long Total);
