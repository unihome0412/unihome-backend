using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Verification;
using UniHome.Infrastructure.Persistence.Mongo;

namespace UniHome.Infrastructure.Persistence.Mongo.Verification;

[ExcludeFromCodeCoverage]
internal sealed class MongoVerificationRepository : IVerificationRepository
{
    private const int DuplicateKeyCode = 11000;

    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoVerificationRepository> _logger;

    public MongoVerificationRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoVerificationRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task AddAsync(
        Expediente expediente,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expediente);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .InsertOneAsync(ToDocument(expediente), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Code == DuplicateKeyCode)
        {
            throw new ExpedienteAlreadyExistsException();
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<Expediente?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(Builders<VerificationExpedienteDocument>.Filter.Eq(
                    item => item.Id,
                    id.ToString("N", CultureInfo.InvariantCulture)))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return document is null ? null : ToDomain(document);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<Expediente?> FindByOwnerAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(Builders<VerificationExpedienteDocument>.Filter.Eq(
                    item => item.OwnerAccountId,
                    ownerAccountId.ToString("N", CultureInfo.InvariantCulture)))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return document is null ? null : ToDomain(document);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<bool> TryReplaceAsync(
        Expediente expediente,
        DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expediente);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<VerificationExpedienteDocument>.Filter.And(
                Builders<VerificationExpedienteDocument>.Filter.Eq(
                    item => item.Id,
                    expediente.Id.ToString("N", CultureInfo.InvariantCulture)),
                Builders<VerificationExpedienteDocument>.Filter.Eq(
                    item => item.UpdatedAt,
                    expectedUpdatedAt.UtcDateTime));
            var result = await collection
                .ReplaceOneAsync(
                    filter,
                    ToDocument(expediente),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return result.MatchedCount == 1;
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<IReadOnlyList<Expediente>> ListAsync(
        EstadoExpediente? state,
        bool suspiciousFirst,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = state is null
                ? Builders<VerificationExpedienteDocument>.Filter.Empty
                : Builders<VerificationExpedienteDocument>.Filter.Eq(
                    item => item.Estado,
                    state.Value.ToString());
            var sort = suspiciousFirst
                ? Builders<VerificationExpedienteDocument>.Sort
                    .Descending(item => item.EsSospechoso)
                    .Ascending(item => item.CreatedAt)
                    .Ascending(item => item.Id)
                : Builders<VerificationExpedienteDocument>.Sort
                    .Descending(item => item.CreatedAt)
                    .Ascending(item => item.Id);
            var documents = await collection
                .Find(filter)
                .Sort(sort)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return documents.Select(ToDomain).ToArray();
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<long> CountAsync(
        EstadoExpediente? state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = state is null
                ? Builders<VerificationExpedienteDocument>.Filter.Empty
                : Builders<VerificationExpedienteDocument>.Filter.Eq(
                    item => item.Estado,
                    state.Value.ToString());

            return await collection
                .CountDocumentsAsync(filter, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<long> CountPendingReviewAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<VerificationExpedienteDocument>.Filter.In(
                item => item.Estado,
                [EstadoExpediente.Pendiente.ToString(), EstadoExpediente.EnRevision.ToString()]);

            return await collection
                .CountDocumentsAsync(filter, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    private async Task<IMongoCollection<VerificationExpedienteDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<VerificationExpedienteDocument>(
                MongoVerificationSchema.ExpedientesCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static VerificationExpedienteDocument ToDocument(Expediente expediente) =>
        new()
        {
            Id = expediente.Id.ToString("N", CultureInfo.InvariantCulture),
            Folio = expediente.Folio,
            OwnerAccountId = expediente.OwnerAccountId.ToString("N", CultureInfo.InvariantCulture),
            PublicationId = expediente.PublicationId.ToString("N", CultureInfo.InvariantCulture),
            PropertyId = expediente.PropertyId.ToString("N", CultureInfo.InvariantCulture),
            Estado = expediente.State.ToString(),
            Ine = ToDocument(expediente.FindDocument(TipoDocumentoVerificacion.Ine)),
            Comprobante = ToDocument(expediente.FindDocument(TipoDocumentoVerificacion.Comprobante)),
            EsSospechoso = expediente.IsSuspicious,
            SenalesRiesgo = [.. expediente.RiskIndicator.Signals],
            ResultadoComparacionNombres = expediente.NameComparison.ToString(),
            MotivoRechazo = expediente.RejectionReason,
            CreatedAt = expediente.CreatedAt.UtcDateTime,
            UpdatedAt = expediente.UpdatedAt.UtcDateTime,
            SubmittedAt = expediente.SubmittedAt?.UtcDateTime,
            Decision = ToDocument(expediente.Decision)
        };

    private static VerificationDocumentMetadataDocument? ToDocument(
        DocumentoVerificacion? document) =>
        document is null
            ? null
            : new VerificationDocumentMetadataDocument
            {
                Tipo = document.Tipo.ToString(),
                StorageKey = document.StorageKey,
                OriginalFileName = document.OriginalFileName,
                ContentType = document.ContentType,
                SizeBytes = document.SizeBytes,
                Sha256 = document.Sha256,
                Version = document.Version,
                UploadedAt = document.UploadedAt.UtcDateTime,
                NombreOcr = document.NombreOcr,
                EstadoOcr = document.EstadoOcr.ToString(),
                EstadoEdicion = document.EstadoEdicion.ToString(),
                EdicionDetectada = document.EdicionDetectada,
                ElaDisponible = document.ElaDisponible,
                MetadatosDisponibles = document.MetadatosDisponibles,
                SenalesEdicion = [.. document.EditionSignals],
                DetalleAnalisis = document.DetalleAnalisis
            };

    private static VerificationDecisionDocument? ToDocument(ExpedienteDecision? decision) =>
        decision is null
            ? null
            : new VerificationDecisionDocument
            {
                Estado = decision.Estado.ToString(),
                ActorAccountId = decision.ActorAccountId.ToString("N", CultureInfo.InvariantCulture),
                DecidedAt = decision.DecidedAt.UtcDateTime,
                Motivo = decision.Motivo
            };

    private static Expediente ToDomain(VerificationExpedienteDocument document) =>
        Expediente.Restore(
            Guid.ParseExact(document.Id, "N"),
            document.Folio,
            Guid.ParseExact(document.OwnerAccountId, "N"),
            Guid.ParseExact(document.PublicationId, "N"),
            Guid.ParseExact(document.PropertyId, "N"),
            Enum.Parse<EstadoExpediente>(document.Estado),
            ToOffset(document.CreatedAt),
            ToOffset(document.UpdatedAt),
            document.SubmittedAt is null ? null : ToOffset(document.SubmittedAt.Value),
            new[] { document.Ine, document.Comprobante }
                .Where(item => item is not null)
                .Select(item => ToDomain(item!))
                .ToArray(),
            document.SenalesRiesgo,
            Enum.Parse<ResultadoComparacionNombres>(document.ResultadoComparacionNombres),
            document.MotivoRechazo,
            ToDomain(document.Decision));

    private static DocumentoVerificacion ToDomain(VerificationDocumentMetadataDocument document) =>
        DocumentoVerificacion.Restore(
            Enum.Parse<TipoDocumentoVerificacion>(document.Tipo),
            document.StorageKey,
            document.OriginalFileName,
            document.ContentType,
            document.SizeBytes,
            document.Sha256,
            document.Version,
            ToOffset(document.UploadedAt),
            document.NombreOcr,
            Enum.Parse<EstadoAnalisisDocumento>(document.EstadoOcr),
            Enum.Parse<EstadoAnalisisDocumento>(document.EstadoEdicion),
            document.EdicionDetectada,
            document.ElaDisponible,
            document.MetadatosDisponibles,
            document.SenalesEdicion,
            document.DetalleAnalisis);

    private static ExpedienteDecision? ToDomain(VerificationDecisionDocument? decision) =>
        decision is null
            ? null
            : new ExpedienteDecision(
                Enum.Parse<EstadoExpediente>(decision.Estado),
                Guid.ParseExact(decision.ActorAccountId, "N"),
                ToOffset(decision.DecidedAt),
                decision.Motivo);

    private static DateTimeOffset ToOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de expedientes en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
