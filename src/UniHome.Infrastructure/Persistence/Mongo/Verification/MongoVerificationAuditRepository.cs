using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Infrastructure.Persistence.Mongo;

namespace UniHome.Infrastructure.Persistence.Mongo.Verification;

[ExcludeFromCodeCoverage]
internal sealed class MongoVerificationAuditRepository : IVerificationAuditRepository
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoVerificationAuditRepository> _logger;

    public MongoVerificationAuditRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoVerificationAuditRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task AppendAsync(
        VerificationAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .InsertOneAsync(
                    new VerificationAuditDocument
                    {
                        Id = entry.Id.ToString("N", CultureInfo.InvariantCulture),
                        ExpedienteId = entry.ExpedienteId.ToString("N", CultureInfo.InvariantCulture),
                        Folio = entry.Folio,
                        ActorAccountId = entry.ActorAccountId.ToString("N", CultureInfo.InvariantCulture),
                        ActorRole = entry.ActorRole,
                        Accion = entry.Action,
                        TipoDocumento = entry.DocumentType?.ToString(),
                        DocumentVersion = entry.DocumentVersion,
                        Detail = entry.Detail,
                        Timestamp = entry.Timestamp.UtcDateTime,
                        TraceId = entry.TraceId
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    private async Task<IMongoCollection<VerificationAuditDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<VerificationAuditDocument>(MongoVerificationSchema.AuditCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La bitácora de verificación no pudo persistirse. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
