using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Verification;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Repositorio en memoria con la misma semántica de unicidad y concurrencia
/// optimista que el adaptador MongoDB, sin requerir un clúster.
/// </summary>
public sealed class InMemoryVerificationRepository : IVerificationRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Expediente> _items = [];

    public Task AddAsync(Expediente expediente, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expediente);

        lock (_gate)
        {
            if (_items.Values.Any(item =>
                    item.OwnerAccountId == expediente.OwnerAccountId ||
                    string.Equals(item.Folio, expediente.Folio, StringComparison.Ordinal)))
            {
                throw new ExpedienteAlreadyExistsException();
            }

            _items.Add(expediente.Id, Clone(expediente));
        }

        return Task.CompletedTask;
    }

    public Task<Expediente?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(
                _items.TryGetValue(id, out var expediente) ? Clone(expediente) : null);
        }
    }

    public Task<Expediente?> FindByOwnerAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var expediente = _items.Values.SingleOrDefault(item =>
                item.OwnerAccountId == ownerAccountId);
            return Task.FromResult(expediente is null ? null : Clone(expediente));
        }
    }

    public Task<bool> TryReplaceAsync(
        Expediente expediente,
        DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expediente);

        lock (_gate)
        {
            if (!_items.TryGetValue(expediente.Id, out var current) ||
                current.UpdatedAt != expectedUpdatedAt)
            {
                return Task.FromResult(false);
            }

            _items[expediente.Id] = Clone(expediente);
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<Expediente>> ListAsync(
        EstadoExpediente? state,
        bool suspiciousFirst,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var query = _items.Values.AsEnumerable();

            if (state is not null)
            {
                query = query.Where(item => item.State == state);
            }

            query = suspiciousFirst
                ? query.OrderByDescending(item => item.IsSuspicious)
                    .ThenBy(item => item.CreatedAt)
                    .ThenBy(item => item.Id)
                : query.OrderByDescending(item => item.CreatedAt)
                    .ThenBy(item => item.Id);

            IReadOnlyList<Expediente> result = query
                .Skip(skip)
                .Take(limit)
                .Select(Clone)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    public Task<long> CountAsync(
        EstadoExpediente? state,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var count = state is null
                ? _items.Count
                : _items.Values.Count(item => item.State == state);
            return Task.FromResult((long)count);
        }
    }

    public Task<long> CountPendingReviewAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult((long)_items.Values.Count(item =>
                item.State is EstadoExpediente.Pendiente or EstadoExpediente.EnRevision));
        }
    }

    public IReadOnlyList<Expediente> All
    {
        get
        {
            lock (_gate)
            {
                return _items.Values.Select(Clone).ToArray();
            }
        }
    }

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
            source.Documents.Select(Clone).ToArray(),
            source.RiskIndicator.Signals,
            source.NameComparison,
            source.RejectionReason,
            source.Decision);

    private static DocumentoVerificacion Clone(DocumentoVerificacion source) =>
        DocumentoVerificacion.Restore(
            source.Tipo,
            source.StorageKey,
            source.OriginalFileName,
            source.ContentType,
            source.SizeBytes,
            source.Sha256,
            source.Version,
            source.UploadedAt,
            source.NombreOcr,
            source.EstadoOcr,
            source.EstadoEdicion,
            source.EdicionDetectada,
            source.ElaDisponible,
            source.MetadatosDisponibles,
            source.EditionSignals,
            source.DetalleAnalisis);
}
