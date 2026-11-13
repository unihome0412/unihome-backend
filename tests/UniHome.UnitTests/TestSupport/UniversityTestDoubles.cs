using UniHome.Application.Universities.Abstractions;
using UniHome.Application.Universities.Exceptions;
using UniHome.Domain.Universities;

namespace UniHome.UnitTests.TestSupport;

internal sealed class FakeUniversityRepository : IUniversityRepository
{
    public List<University> Items { get; } = [];

    public string? LastSearch { get; private set; }

    public int LastLimit { get; private set; }

    public bool ThrowDuplicate { get; set; }

    public bool ThrowDuplicateOnUpdate { get; set; }

    public bool UpdateResult { get; set; } = true;

    public bool SetActiveResult { get; set; } = true;

    public Exception? AddException { get; set; }

    public int LastPage { get; private set; }

    public int LastPageSize { get; private set; }

    public Task AddAsync(
        University university,
        CancellationToken cancellationToken = default)
    {
        if (ThrowDuplicate)
        {
            throw new DuplicateUniversityException();
        }

        if (AddException is not null)
        {
            throw AddException;
        }

        Items.Add(university);
        return Task.CompletedTask;
    }

    public Task<University?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.SingleOrDefault(university => university.Id == id));

    public Task<UniversityPage> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        LastPage = page;
        LastPageSize = pageSize;

        var ordered = Items
            .OrderBy(university => university.NormalizedName, StringComparer.Ordinal)
            .ToArray();
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult(new UniversityPage(items, ordered.Length));
    }

    public Task<bool> UpdateNameAndCampusAsync(
        University university,
        string expectedNormalizedName,
        CancellationToken cancellationToken = default)
    {
        if (ThrowDuplicateOnUpdate)
        {
            throw new DuplicateUniversityException();
        }

        return Task.FromResult(UpdateResult);
    }

    public Task<bool> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (!SetActiveResult)
        {
            return Task.FromResult(false);
        }

        var university = Items.SingleOrDefault(item => item.Id == id);

        if (university is null)
        {
            return Task.FromResult(false);
        }

        university.SetActive(isActive);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<University>> SearchActiveAsync(
        string normalizedTerm,
        int limit,
        CancellationToken cancellationToken = default)
    {
        LastSearch = normalizedTerm;
        LastLimit = limit;

        IReadOnlyList<University> results = Items
            .Where(university => university.IsActive)
            .Where(university => university.NormalizedName.Contains(
                normalizedTerm,
                StringComparison.Ordinal))
            .Take(limit)
            .ToArray();

        return Task.FromResult(results);
    }
}
