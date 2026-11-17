using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Verification;

namespace UniHome.UnitTests.TestSupport;

internal sealed class FakeVerificationRepository : IVerificationRepository
{
    private readonly Dictionary<Guid, Expediente> _items = [];

    public int ReplaceCalls { get; private set; }

    public bool ReplaceResult { get; set; } = true;

    public Task AddAsync(Expediente expediente, CancellationToken cancellationToken = default)
    {
        if (_items.Values.Any(item =>
                item.OwnerAccountId == expediente.OwnerAccountId ||
                item.Folio == expediente.Folio))
        {
            throw new ExpedienteAlreadyExistsException();
        }

        _items[expediente.Id] = Clone(expediente);
        return Task.CompletedTask;
    }

    public Task<Expediente?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.TryGetValue(id, out var item) ? Clone(item) : null);

    public Task<Expediente?> FindByOwnerAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken = default)
    {
        var item = _items.Values.SingleOrDefault(expediente =>
            expediente.OwnerAccountId == ownerAccountId);
        return Task.FromResult(item is null ? null : Clone(item));
    }

    public Task<IReadOnlySet<Guid>> ListApprovedOwnerIdsAsync(
        IReadOnlyCollection<Guid> ownerAccountIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(_items.Values
            .Where(item =>
                item.State == EstadoExpediente.Aprobado &&
                ownerAccountIds.Contains(item.OwnerAccountId))
            .Select(item => item.OwnerAccountId)
            .ToHashSet());

    public Task<bool> TryReplaceAsync(
        Expediente expediente,
        DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        ReplaceCalls++;

        if (!ReplaceResult ||
            !_items.TryGetValue(expediente.Id, out var current) ||
            current.UpdatedAt != expectedUpdatedAt)
        {
            return Task.FromResult(false);
        }

        _items[expediente.Id] = Clone(expediente);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<Expediente>> ListAsync(
        EstadoExpediente? state,
        bool suspiciousFirst,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _items.Values.AsEnumerable();

        if (state is not null)
        {
            query = query.Where(item => item.State == state);
        }

        query = suspiciousFirst
            ? query.OrderByDescending(item => item.IsSuspicious).ThenBy(item => item.CreatedAt)
            : query.OrderByDescending(item => item.CreatedAt);

        IReadOnlyList<Expediente> result = query.Skip(skip).Take(limit).Select(Clone).ToArray();
        return Task.FromResult(result);
    }

    public Task<long> CountAsync(
        EstadoExpediente? state,
        CancellationToken cancellationToken = default) =>
        Task.FromResult((long)(state is null
            ? _items.Count
            : _items.Values.Count(item => item.State == state)));

    public Task<long> CountPendingReviewAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult((long)_items.Values.Count(item =>
            item.State is EstadoExpediente.Pendiente or EstadoExpediente.EnRevision));

    public IReadOnlyList<Expediente> All => _items.Values.Select(Clone).ToArray();

    private static Expediente Clone(Expediente source) =>
        Expediente.Restore(
            source.Id,
            source.Folio,
            source.OwnerAccountId,
            source.PublicationId,
            source.PropertyId,
            source.State,
            source.CreatedAt,
            source.UpdatedAt,
            source.SubmittedAt,
            source.Documents.Select(document => DocumentoVerificacion.Restore(
                document.Tipo,
                document.StorageKey,
                document.OriginalFileName,
                document.ContentType,
                document.SizeBytes,
                document.Sha256,
                document.Version,
                document.UploadedAt,
                document.NombreOcr,
                document.EstadoOcr,
                document.EstadoEdicion,
                document.EdicionDetectada,
                document.ElaDisponible,
                document.MetadatosDisponibles,
                document.EditionSignals,
                document.DetalleAnalisis)).ToArray(),
            source.RiskIndicator.Signals,
            source.NameComparison,
            source.RejectionReason,
            source.Decision,
            source.NameComparisonUpdatedAt);
}

internal sealed class FakeVerificationDocumentStore : IVerificationDocumentStore
{
    private readonly Dictionary<string, VerificationDocumentContent> _items = [];

    public bool ThrowOnSave { get; set; }

    public int SaveCalls { get; private set; }

    public IReadOnlyCollection<string> StoredKeys => _items.Keys.ToArray();

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
        SaveCalls++;

        if (ThrowOnSave)
        {
            throw new UniHome.Application.Identity.Exceptions.StorageUnavailableException();
        }

        var key = $"doc-{Guid.NewGuid():N}";
        _items[key] = new VerificationDocumentContent(
            [.. content],
            contentType,
            content.LongLength,
            sha256);
        return Task.FromResult(new StoredVerificationDocument(
            key,
            contentType,
            content.LongLength,
            sha256));
    }

    public Task<VerificationDocumentContent?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.TryGetValue(storageKey, out var content) ? content : null);

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        _items.Remove(storageKey);
        return Task.CompletedTask;
    }
}

internal sealed class FakeVerificationAuditRepository : IVerificationAuditRepository
{
    public List<VerificationAuditEntry> Entries { get; } = [];

    public bool ThrowOnAppend { get; set; }

    public Task AppendAsync(
        VerificationAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnAppend)
        {
            throw new UniHome.Application.Identity.Exceptions.StorageUnavailableException();
        }

        Entries.Add(entry);
        return Task.CompletedTask;
    }
}

internal sealed class FakeDocumentAnalyzer : IDocumentAnalyzer
{
    public DocumentAnalysisResult Result { get; set; } = new(
        "Ana Pérez",
        EstadoAnalisisDocumento.Completado,
        EstadoAnalisisDocumento.Completado,
        EditionDetected: false,
        EditionSignals: [],
        ElaAvailable: true,
        MetadataAvailable: true,
        UnavailableDetail: null);

    public bool Throw { get; set; }

    public Task<DocumentAnalysisResult> AnalyzeAsync(
        ReadOnlyMemory<byte> content,
        string contentType,
        TipoDocumentoVerificacion documentType,
        CancellationToken cancellationToken = default) =>
        Throw
            ? throw new InvalidOperationException("analyzer")
            : Task.FromResult(Result);
}

internal sealed class FakeDocumentUrlSigner : IDocumentUrlSigner
{
    public DocumentAccessGrant Create(
        Guid expedienteId,
        TipoDocumentoVerificacion documentType,
        int version,
        DocumentUrlVariant variant,
        DateTimeOffset now) =>
        new($"token-{expedienteId:N}-{documentType}-{version}-{variant}", now.AddMinutes(5));

    public DocumentAccessToken? Validate(string token, DateTimeOffset now) =>
        token.StartsWith("token-", StringComparison.Ordinal)
            ? new DocumentAccessToken(
                Guid.ParseExact(token.Split('-')[1], "N"),
                token.Contains("Ine", StringComparison.Ordinal)
                    ? TipoDocumentoVerificacion.Ine
                    : TipoDocumentoVerificacion.Comprobante,
                int.Parse(token.Split('-')[3]),
                token.EndsWith("Thumbnail", StringComparison.Ordinal)
                    ? DocumentUrlVariant.Thumbnail
                    : DocumentUrlVariant.Full,
                now.AddMinutes(1))
            : null;
}

internal sealed class FakeDocumentAccessUrlBuilder : IDocumentAccessUrlBuilder
{
    public string Build(string token) => $"/api/v1/expedientes/documentos/{token}";
}

internal sealed class FakeDocumentPreviewGenerator : IDocumentPreviewGenerator
{
    public Task<DocumentPreviewContent> CreateAsync(
        VerificationDocumentContent document,
        bool thumbnail,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new DocumentPreviewContent(
            document.Content,
            document.ContentType,
            thumbnail));
}
