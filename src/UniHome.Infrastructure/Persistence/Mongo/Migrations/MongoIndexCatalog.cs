using MongoDB.Bson;

namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

/// <summary>Comprobación sin escrituras compartida por migrador y API.</summary>
internal static class MongoIndexCatalog
{
    public static (IReadOnlyList<MongoIndexDefinition> Missing, IReadOnlyList<string> Issues) Inspect(
        MongoCollectionDefinition collection,
        IReadOnlyList<BsonDocument> indexes)
    {
        var missing = new List<MongoIndexDefinition>();
        var issues = new List<string>();
        foreach (var desired in collection.Indexes)
        {
            var actual = indexes.FirstOrDefault(index => Name(index) == desired.Name);
            if (actual is not null)
            {
                if (!desired.Matches(actual))
                {
                    issues.Add($"Índice divergente: {collection.Name}/{desired.Name}.");
                }
            }
            else if (indexes.Any(index => index.TryGetValue("key", out var key) &&
                         key.IsBsonDocument && desired.KeyMatches(key.AsBsonDocument)))
            {
                issues.Add($"Clave con otro nombre: {collection.Name}/{desired.Name}.");
            }
            else
            {
                missing.Add(desired);
            }
        }

        foreach (var index in indexes.Where(index => Name(index) != "_id_" &&
                     !collection.Indexes.Any(desired => desired.Name == Name(index))))
        {
            // Un TTL no previsto puede borrar cuentas; un unique adicional cambia
            // las restricciones de negocio. Ninguno se acepta silenciosamente.
            if (index.Contains("expireAfterSeconds") ||
                index.GetValue("unique", BsonBoolean.False).ToBoolean())
            {
                issues.Add($"Índice restrictivo o TTL no previsto: {collection.Name}/{Name(index)}.");
            }
        }

        return (missing, issues);
    }

    private static string Name(BsonDocument index) =>
        index.TryGetValue("name", out var value) && value.IsString ? value.AsString : string.Empty;
}
