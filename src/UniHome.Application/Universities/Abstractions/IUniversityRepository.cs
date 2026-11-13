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

    Task<IReadOnlyList<University>> SearchActiveAsync(
        string normalizedTerm,
        int limit,
        CancellationToken cancellationToken = default);
}
