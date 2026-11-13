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

    public Task<University?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(
                _universities.Values.SingleOrDefault(university => university.Id == id));
        }
    }

    public Task<UniversityPage> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        lock (_gate)
        {
            var ordered = _universities.Values
                .OrderBy(university => university.NormalizedName, StringComparer.Ordinal)
                .ToArray();
            var items = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            return Task.FromResult(new UniversityPage(items, ordered.Length));
        }
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

    public Task<bool> UpdateNameAndCampusAsync(
        University university,
        string expectedNormalizedName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(university);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedNormalizedName);

        lock (_gate)
        {
            if (!_universities.TryGetValue(expectedNormalizedName, out var current) ||
                current.Id != university.Id)
            {
                return Task.FromResult(false);
            }

            if (_universities.TryGetValue(university.NormalizedName, out var conflict) &&
                conflict.Id != university.Id)
            {
                throw new DuplicateUniversityException();
            }

            _universities.Remove(expectedNormalizedName);
            _universities[university.NormalizedName] = university;
            return Task.FromResult(true);
        }
    }

    public Task<bool> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var university = _universities.Values.SingleOrDefault(item => item.Id == id);

            if (university is null)
            {
                return Task.FromResult(false);
            }

            university.SetActive(isActive);
            return Task.FromResult(true);
        }
    }
}
