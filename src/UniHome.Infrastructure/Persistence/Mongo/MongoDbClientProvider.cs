using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Persistence.Mongo;

/// <summary>
/// Crea el cliente de MongoDB de forma perezosa. Si no hay configuración,
/// no se crea ningún cliente ni se intenta abrir una conexión al iniciar la API.
/// </summary>
internal sealed class MongoDbClientProvider : IDisposable
{
    private readonly IOptions<MongoDbOptions> _options;
    private readonly Lazy<MongoClient>? _client;
    private bool _disposed;

    public MongoDbClientProvider(IOptions<MongoDbOptions> options)
    {
        _options = options;

        if (Current.IsConfigured)
        {
            _client = new Lazy<MongoClient>(CreateClient, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    private MongoDbOptions Current => _options.Value;

    public bool IsConfigured => Current.IsConfigured;

    public IMongoDatabase GetDatabase()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_client is null)
        {
            throw new InvalidOperationException("MongoDB no está configurado.");
        }

        return _client.Value.GetDatabase(Current.DatabaseName!.Trim());
    }

    private MongoClient CreateClient()
    {
        var settings = MongoClientSettings.FromConnectionString(Current.ConnectionString!.Trim());
        settings.ServerSelectionTimeout = Current.GetServerSelectionTimeout();
        settings.ApplicationName = "UniHome.Api";

        return new MongoClient(settings);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_client is { IsValueCreated: true })
        {
            _client.Value.Dispose();
        }
    }
}
