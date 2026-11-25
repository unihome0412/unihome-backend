using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Rentals.Abstractions;
using UniHome.Domain.Rentals;

namespace UniHome.Infrastructure.Persistence.Mongo.Rentals;

[ExcludeFromCodeCoverage]
internal sealed class MongoInformativePaymentRepository : IInformativePaymentRepository
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoInformativePaymentRepository> _logger;

    public MongoInformativePaymentRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoInformativePaymentRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task SaveAsync(InformativePayment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .ReplaceOneAsync(
                    Builders<InformativePaymentDocument>.Filter.Eq(
                        item => item.Id,
                        payment.Id.ToString("N", CultureInfo.InvariantCulture)),
                    ToDocument(payment),
                    new ReplaceOptions { IsUpsert = true },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<IReadOnlyList<InformativePayment>> ListByRentPeriodAsync(
        Guid rentPeriodId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var documents = await collection
                .Find(Builders<InformativePaymentDocument>.Filter.Eq(
                    item => item.RentPeriodId,
                    rentPeriodId.ToString("N", CultureInfo.InvariantCulture)))
                .Sort(Builders<InformativePaymentDocument>.Sort.Ascending(item => item.ReceivedOn))
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

    private async Task<IMongoCollection<InformativePaymentDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<InformativePaymentDocument>(MongoRentalsSchema.PaymentsCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static InformativePaymentDocument ToDocument(InformativePayment payment) =>
        new()
        {
            Id = payment.Id.ToString("N", CultureInfo.InvariantCulture),
            RentPeriodId = payment.RentPeriodId.ToString("N", CultureInfo.InvariantCulture),
            TenantAccountId = payment.TenantAccountId.ToString("N", CultureInfo.InvariantCulture),
            AmountMxn = payment.AmountMxn,
            ReceivedOn = payment.ReceivedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            RegisteredAt = payment.RegisteredAt.UtcDateTime
        };

    private static InformativePayment ToDomain(InformativePaymentDocument document) =>
        InformativePayment.Restore(
            Guid.ParseExact(document.Id, "N"),
            Guid.ParseExact(document.RentPeriodId, "N"),
            Guid.ParseExact(document.TenantAccountId, "N"),
            document.AmountMxn,
            DateOnly.FromDateTime(document.ReceivedOn),
            new DateTimeOffset(DateTime.SpecifyKind(document.RegisteredAt, DateTimeKind.Utc)));

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de pago informativo en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
