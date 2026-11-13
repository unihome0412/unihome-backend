using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.Infrastructure.Persistence.Mongo.PasswordResets;
using UniHome.Infrastructure.Persistence.Mongo.Publications;
using UniHome.Infrastructure.Persistence.Mongo.Universities;

namespace UniHome.Infrastructure.Persistence.Mongo;

/// <summary>
/// Fail-closed sin DDL ni caché: verifica índices antes de utilizar cada colección
/// operativa (identidad, recuperación y universidades). La identidad de ejecución
/// necesita listIndexes, no createIndex/createCollection.
/// </summary>
internal static class MongoIdentitySchemaGuard
{
    private static readonly MongoCollectionDefinition[] Definitions =
    [
        .. MongoIdentityMigration.Definition.Collections,
        MongoPasswordResetIndexes.Definition,
        MongoUniversityIndexes.Definition,
        .. MongoPublicationIndexes.Definitions
    ];

    public static async Task EnsureAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        CancellationToken cancellationToken = default)
    {
        var collectionName = collection.CollectionNamespace.CollectionName;
        var definition = Definitions.SingleOrDefault(item => item.Name == collectionName)
            ?? throw new StorageUnavailableException();

        using var cursor = await collection.Indexes.ListAsync(cancellationToken).ConfigureAwait(false);
        var indexes = new List<BsonDocument>();
        while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
        {
            indexes.AddRange(cursor.Current);
        }

        var inspection = MongoIndexCatalog.Inspect(definition, indexes);
        if (inspection.Missing.Count > 0 || inspection.Issues.Count > 0)
        {
            throw new StorageUnavailableException();
        }
    }
}
