using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Application.Abstractions.Storage;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Persistence.Mongo;

/// <summary>
/// Adaptador de readiness para MongoDB. No devuelve cadenas de conexión,
/// nombres de servidor ni mensajes de excepción al consumidor.
/// </summary>
internal sealed class MongoStorageReadinessProbe : IStorageReadinessProbe
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly IOptions<MongoDbOptions> _options;
    private readonly ILogger<MongoStorageReadinessProbe> _logger;

    public MongoStorageReadinessProbe(
        MongoDbClientProvider clientProvider,
        IOptions<MongoDbOptions> options,
        ILogger<MongoStorageReadinessProbe> logger)
    {
        _clientProvider = clientProvider;
        _options = options;
        _logger = logger;
    }

    public async Task<StorageReadiness> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!_clientProvider.IsConfigured)
        {
            return StorageReadiness.NotConfigured();
        }

        try
        {
            var database = _clientProvider.GetDatabase();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.Value.GetServerSelectionTimeout());

            var ping = new BsonDocumentCommand<BsonDocument>(new BsonDocument("ping", 1));
            await database
                .RunCommandAsync(ping, ReadPreference.Primary, timeout.Token)
                .ConfigureAwait(false);

            await MongoIdentitySchemaGuard.EnsureAsync(
                database.GetCollection<BsonDocument>(MongoIdentitySchema.AccountsCollection), timeout.Token)
                .ConfigureAwait(false);
            await MongoIdentitySchemaGuard.EnsureAsync(
                database.GetCollection<BsonDocument>(MongoIdentitySchema.SessionsCollection), timeout.Token)
                .ConfigureAwait(false);

            return StorageReadiness.Ready();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "La comprobación de MongoDB superó el tiempo límite. TraceId={TraceId}",
                Activity.Current?.Id);

            return StorageReadiness.Unavailable("MongoDB no respondió dentro del tiempo límite.");
        }
        catch (Exception exception)
        {
            // No se registra el mensaje de la excepción: puede contener datos de conexión.
            _logger.LogWarning(
                "La comprobación de MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
                exception.GetType().FullName,
                Activity.Current?.Id);

            return StorageReadiness.Unavailable("Almacenamiento no disponible o esquema de identidad no preparado.");
        }
    }
}
