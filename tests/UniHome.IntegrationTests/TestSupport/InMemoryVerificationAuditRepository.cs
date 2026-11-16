using UniHome.Application.Verification.Abstractions;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryVerificationAuditRepository : IVerificationAuditRepository
{
    private readonly object _gate = new();
    private readonly List<VerificationAuditEntry> _items = [];

    public Task AppendAsync(
        VerificationAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _items.Add(entry);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<VerificationAuditEntry> All
    {
        get
        {
            lock (_gate)
            {
                return _items.ToArray();
            }
        }
    }
}
