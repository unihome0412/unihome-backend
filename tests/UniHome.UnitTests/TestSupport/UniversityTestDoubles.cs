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

    public Exception? AddException { get; set; }

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
