using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Publications.Abstractions;
using UniHome.Application.Publications.Exceptions;
using UniHome.Domain.Publications;

namespace UniHome.Infrastructure.Persistence.Mongo.Publications;

[ExcludeFromCodeCoverage]
internal sealed class MongoTermsAcceptanceRepository : ITermsAcceptanceRepository
{
    private const int DuplicateKeyCode = 11000;

    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoTermsAcceptanceRepository> _logger;

    public MongoTermsAcceptanceRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoTermsAcceptanceRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task AddAsync(
        TermsAcceptance acceptance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acceptance);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .InsertOneAsync(
                    new TermsAcceptanceDocument
                    {
                        Id = acceptance.Id.ToString("N", CultureInfo.InvariantCulture),
                        AccountId = acceptance.AccountId.ToString("N", CultureInfo.InvariantCulture),
                        Version = acceptance.Version,
                        AcceptedAt = acceptance.AcceptedAt.UtcDateTime
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoWriteException exception) when (IsDuplicateKey(exception))
        {
            throw new DuplicateTermsAcceptanceException();
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<bool> ExistsAsync(
        Guid accountId,
        string version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var count = await collection.CountDocumentsAsync(
                Builders<TermsAcceptanceDocument>.Filter.And(
                    Builders<TermsAcceptanceDocument>.Filter.Eq(
                        document => document.AccountId,
                        accountId.ToString("N", CultureInfo.InvariantCulture)),
                    Builders<TermsAcceptanceDocument>.Filter.Eq(
                        document => document.Version,
                        version)),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return count > 0;
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    private async Task<IMongoCollection<TermsAcceptanceDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<TermsAcceptanceDocument>(MongoPublicationSchema.TermsCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static bool IsDuplicateKey(MongoWriteException exception) =>
        exception.WriteError?.Code == DuplicateKeyCode;

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de términos en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
