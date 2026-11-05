using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo;

/// <summary>
/// Fail-closed sin DDL ni caché: verifica índices antes de utilizar cada colección.
/// La identidad de ejecución necesita listIndexes, no createIndex/createCollection.
/// </summary>
internal static class MongoIdentitySchemaGuard
{
    public static async Task EnsureAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        CancellationToken cancellationToken = default)
    {
        var definition = MongoIdentityMigration.Definition.Collections.Single(
            item => item.Name == collection.CollectionNamespace.CollectionName);
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
