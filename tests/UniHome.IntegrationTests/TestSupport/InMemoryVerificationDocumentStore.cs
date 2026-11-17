using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Verification;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryVerificationDocumentStore : IVerificationDocumentStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, VerificationDocumentContent> _items = [];

    public Task<StoredVerificationDocument> SaveAsync(
        Guid expedienteId,
        TipoDocumentoVerificacion type,
        int version,
        byte[] content,
        string contentType,
        string originalFileName,
        string sha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var storageKey = $"doc-{Guid.NewGuid():N}";

        lock (_gate)
        {
            _items[storageKey] = new VerificationDocumentContent(
                [.. content],
                contentType,
                content.LongLength,
                sha256.ToUpperInvariant());
        }

        return Task.FromResult(new StoredVerificationDocument(
            storageKey,
            contentType,
            content.LongLength,
            sha256.ToUpperInvariant()));
    }

    public Task<VerificationDocumentContent?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(
                _items.TryGetValue(storageKey, out var content) ? content : null);
        }
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _items.Remove(storageKey);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<string, VerificationDocumentContent> All
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, VerificationDocumentContent>(_items);
            }
        }
    }
}
