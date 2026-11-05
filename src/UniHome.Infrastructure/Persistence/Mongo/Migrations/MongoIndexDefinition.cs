using MongoDB.Bson;
using MongoDB.Driver;

namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

public sealed record MongoIndexField(string Name, int Direction);

/// <summary>
/// Definición declarativa de un índice. La migración la usa para crear el índice
/// y para comprobar que una definición preexistente no diverge.
/// </summary>
public sealed class MongoIndexDefinition
{
    public MongoIndexDefinition(
        string name,
        IReadOnlyList<MongoIndexField> fields,
        bool unique = false,
        int? expireAfterSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del índice es obligatorio.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(fields);

        if (fields.Count == 0)
        {
            throw new ArgumentException("El índice debe tener al menos un campo.", nameof(fields));
        }

        if (fields.Any(field => field.Direction is not (1 or -1)))
        {
            throw new ArgumentException("La dirección de un índice debe ser 1 o -1.", nameof(fields));
        }

        if (expireAfterSeconds is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expireAfterSeconds),
                "El TTL no puede ser negativo.");
        }

        Name = name;
        Fields = fields.ToArray();
        Unique = unique;
        ExpireAfterSeconds = expireAfterSeconds;
    }

    public string Name { get; }

    public IReadOnlyList<MongoIndexField> Fields { get; }

    public bool Unique { get; }

    public int? ExpireAfterSeconds { get; }

    public BsonDocument ToKeyDocument() =>
        new(Fields.Select(field => new BsonElement(field.Name, field.Direction)));

    public string ToKeyString() => ToKeyDocument().ToJson();

    public CreateIndexModel<BsonDocument> ToCreateIndexModel()
    {
        var options = new CreateIndexOptions
        {
            Name = Name,
            Unique = Unique
        };

        if (ExpireAfterSeconds.HasValue)
        {
            options.ExpireAfter = TimeSpan.FromSeconds(ExpireAfterSeconds.Value);
        }

        return new CreateIndexModel<BsonDocument>(
            new BsonDocumentIndexKeysDefinition<BsonDocument>(ToKeyDocument()),
            options);
    }

    /// <summary>
    /// Compara el índice deseado con el documento devuelto por listIndexes.
    /// Rechaza diferencias en clave, unicidad, TTL, filtro parcial, colación,
    /// sparse u oculto: son opciones que cambian la garantía del índice.
    /// </summary>
    public bool Matches(BsonDocument existing)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (!existing.TryGetValue("key", out var keyValue) || !keyValue.IsBsonDocument)
        {
            return false;
        }

        if (!KeyMatches(keyValue.AsBsonDocument))
        {
            return false;
        }

        var existingUnique = existing.GetValue("unique", BsonBoolean.False).ToBoolean();

        if (existingUnique != Unique)
        {
            return false;
        }

        if (ExpireAfterSeconds.HasValue)
        {
            if (!existing.TryGetValue("expireAfterSeconds", out var ttl) ||
                !ttl.IsNumeric ||
                ttl.ToDouble() != ExpireAfterSeconds.Value)
            {
                return false;
            }
        }
        else if (existing.Contains("expireAfterSeconds"))
        {
            return false;
        }

        return !existing.Contains("partialFilterExpression") &&
               !existing.Contains("collation") &&
               !existing.GetValue("sparse", BsonBoolean.False).ToBoolean() &&
               !existing.GetValue("hidden", BsonBoolean.False).ToBoolean();
    }

    public bool KeyMatches(BsonDocument existingKey)
    {
        ArgumentNullException.ThrowIfNull(existingKey);

        if (existingKey.ElementCount != Fields.Count)
        {
            return false;
        }

        for (var index = 0; index < Fields.Count; index++)
        {
            var expected = Fields[index];
            var actual = existingKey.GetElement(index);

            if (!string.Equals(actual.Name, expected.Name, StringComparison.Ordinal) ||
                !actual.Value.IsNumeric ||
                actual.Value.ToDouble() != expected.Direction)
            {
                return false;
            }
        }

        return true;
    }
}
