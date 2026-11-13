using UniHome.Application.Universities.Abstractions;
using UniHome.Application.Universities.Exceptions;
using UniHome.Domain.Universities;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Repositorio de integración en memoria que reproduce la unicidad del nombre
/// normalizado y el filtro de activas del adaptador MongoDB.
/// </summary>
public sealed class InMemoryUniversityRepository : IUniversityRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, University> _universities =
        new(StringComparer.Ordinal);

    public IReadOnlyList<University> All
    {
        get
        {
            lock (_gate)
            {
                return _universities.Values.ToArray();
            }
        }
    }

    public Task AddAsync(
        University university,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(university);

        lock (_gate)
        {
            if (!_universities.TryAdd(university.NormalizedName, university))
            {
                throw new DuplicateUniversityException();
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<University>> SearchActiveAsync(
        string normalizedTerm,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedTerm);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        lock (_gate)
        {
            var results = _universities.Values
                .Where(university =>
                    university.IsActive &&
                    university.NormalizedName.Contains(normalizedTerm, StringComparison.Ordinal))
                .OrderBy(university => university.NormalizedName, StringComparer.Ordinal)
                .Take(limit)
                .ToArray();

            return Task.FromResult<IReadOnlyList<University>>(results);
        }
    }
}
