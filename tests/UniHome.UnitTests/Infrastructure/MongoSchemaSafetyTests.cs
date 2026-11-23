using System.Reflection;
using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Chat;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoSchemaSafetyTests
{
    private static MongoCollectionDefinition Accounts => MongoIdentityMigration.Definition.Collections.Single(
        collection => collection.Name == MongoIdentitySchema.AccountsCollection);

    [Theory]
    [InlineData("{ name: 'ttl_incorrecto', key: { creadoEnUtc: 1 }, expireAfterSeconds: 0 }")]
    [InlineData("{ name: 'ux_incorrecto', key: { nombreCompleto: 1 }, unique: true }")]
    public void Catalogo_DebeRechazarIndicesAdicionalesPeligrosos(string extra)
    {
        var indexes = ValidAccountIndexes();
        indexes.Add(BsonDocument.Parse(extra));
        var inspection = MongoIndexCatalog.Inspect(Accounts, indexes);
        Assert.Empty(inspection.Missing);
        Assert.NotEmpty(inspection.Issues);
    }

    [Fact]
    public void Catalogo_DebeDetectarIndiceUnicoAusente()
    {
        var inspection = MongoIndexCatalog.Inspect(Accounts, []);
        Assert.Single(inspection.Missing);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Guard_DebeBloquearSinIndiceYSoloLeer(bool valid)
    {
        var reads = 0;
        var manager = InterfaceProxy<IMongoIndexManager<BsonDocument>>.Create((method, _) =>
        {
            Assert.Equal("ListAsync", method.Name);
            reads++;
            return Task.FromResult<IAsyncCursor<BsonDocument>>(new IndexCursor(valid ? ValidAccountIndexes() : []));
        });
        var collection = InterfaceProxy<IMongoCollection<BsonDocument>>.Create((method, _) => method.Name switch
        {
            "get_CollectionNamespace" => new CollectionNamespace(new DatabaseNamespace("unit_test"), Accounts.Name),
            "get_Indexes" => manager,
            _ => throw new InvalidOperationException("El guard no debe escribir ni consultar documentos.")
        });

        if (valid)
        {
            await MongoIdentitySchemaGuard.EnsureAsync(collection);
        }
        else
        {
            await Assert.ThrowsAsync<StorageUnavailableException>(() => MongoIdentitySchemaGuard.EnsureAsync(collection));
        }
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task Guard_DebeReconocerColeccionesDeChatM08()
    {
        foreach (var definition in MongoChatMigration.Definition.Collections)
        {
            var reads = 0;
            var manager = InterfaceProxy<IMongoIndexManager<BsonDocument>>.Create((method, _) =>
            {
                Assert.Equal("ListAsync", method.Name);
                reads++;
                return Task.FromResult<IAsyncCursor<BsonDocument>>(
                    new IndexCursor(ValidIndexes(definition)));
            });
            var collection = InterfaceProxy<IMongoCollection<BsonDocument>>.Create((method, _) =>
                method.Name switch
                {
                    "get_CollectionNamespace" => new CollectionNamespace(
                        new DatabaseNamespace("unit_test"),
                        definition.Name),
                    "get_Indexes" => manager,
                    _ => throw new InvalidOperationException(
                        "El guard no debe escribir ni consultar documentos.")
                });

            await MongoIdentitySchemaGuard.EnsureAsync(collection);

            Assert.Equal(1, reads);
        }
    }

    [Fact]
    public void Ledger_DebeRechazarChecksumDistintoSinModificarRegistro()
    {
        var existing = new BsonDocument("checksumSha256", "checksum_anterior");
        var before = existing.DeepClone();
        Assert.Throws<MongoMigrationDivergenceException>(() =>
            MongoMigrationRunner.ValidateRecordedChecksum(existing, MongoIdentityMigration.Definition));
        Assert.Equal(before, existing);
    }

    [Fact]
    public void Ledger_DebeAceptarChecksumGanadorIdentico()
    {
        var existing = new BsonDocument("checksumSha256", MongoIdentityMigration.Definition.Checksum);
        MongoMigrationRunner.ValidateRecordedChecksum(existing, MongoIdentityMigration.Definition);
    }

    private static List<BsonDocument> ValidAccountIndexes() => ValidIndexes(Accounts);

    private static List<BsonDocument> ValidIndexes(MongoCollectionDefinition definition)
    {
        var indexes = new List<BsonDocument>
        {
            BsonDocument.Parse("{ name: '_id_', key: { _id: 1 } }")
        };

        foreach (var index in definition.Indexes)
        {
            var document = new BsonDocument
            {
                ["name"] = index.Name,
                ["key"] = index.ToKeyDocument()
            };

            if (index.Unique)
            {
                document["unique"] = true;
            }

            if (index.ExpireAfterSeconds is not null)
            {
                document["expireAfterSeconds"] = index.ExpireAfterSeconds.Value;
            }

            indexes.Add(document);
        }

        return indexes;
    }

    private sealed class IndexCursor(IReadOnlyList<BsonDocument> documents) : IAsyncCursor<BsonDocument>
    {
        private bool _consumed;
        public IEnumerable<BsonDocument> Current => documents;
        public bool MoveNext(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = !_consumed;
            _consumed = true;
            return result;
        }
        public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(MoveNext(cancellationToken));
        public void Dispose() { }
    }

    public class InterfaceProxy<T> : DispatchProxy where T : class
    {
        private Func<MethodInfo, object?[]?, object?> _handler = null!;
        public static T Create(Func<MethodInfo, object?[]?, object?> handler)
        {
            var proxy = DispatchProxy.Create<T, InterfaceProxy<T>>();
            ((InterfaceProxy<T>)(object)proxy)._handler = handler;
            return proxy;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => _handler(targetMethod!, args);
    }
}
