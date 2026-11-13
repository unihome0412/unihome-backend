using UniHome.Application.Publications.Abstractions;
using UniHome.Application.Publications.Exceptions;
using UniHome.Domain.Publications;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryTermsAcceptanceRepository : ITermsAcceptanceRepository
{
    private readonly object _gate = new();
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
    private readonly List<TermsAcceptance> _items = [];

    public Task AddAsync(
        TermsAcceptance acceptance,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var key = $"{acceptance.AccountId:N}|{acceptance.Version}";

            if (!_keys.Add(key))
            {
                throw new DuplicateTermsAcceptanceException();
            }

            _items.Add(acceptance);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(
        Guid accountId,
        string version,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_keys.Contains($"{accountId:N}|{version}"));
        }
    }
}
