using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

public sealed class MongoCollectionDefinition
{
    public MongoCollectionDefinition(
        string name,
        IReadOnlyList<MongoIndexDefinition> indexes,
        MongoValidatorDefinition? validator = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre de la colección es obligatorio.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(indexes);

        Name = name;
        Indexes = indexes.ToArray();
        Validator = validator;
    }

    public string Name { get; }

    public IReadOnlyList<MongoIndexDefinition> Indexes { get; }

    public MongoValidatorDefinition? Validator { get; }
}

/// <summary>
/// Migración versionada. El checksum cubre colecciones, campos y opciones de
/// índice; si la definición cambia después de aplicarse, la migración se rechaza
/// en lugar de modificar índices existentes silenciosamente.
/// </summary>
public sealed class MongoMigrationDefinition
{
    public MongoMigrationDefinition(
        string version,
        string name,
        string description,
        IReadOnlyList<MongoCollectionDefinition> collections)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("La versión de la migración es obligatoria.", nameof(version));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre de la migración es obligatorio.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(collections);

        Version = version;
        Name = name;
        Description = description ?? string.Empty;
        Collections = collections.ToArray();
        Checksum = ComputeChecksum();
    }

    public string Version { get; }

    public string Name { get; }

    public string Description { get; }

    public IReadOnlyList<MongoCollectionDefinition> Collections { get; }

    public string Checksum { get; }

    private string ComputeChecksum()
    {
        var payload = new
        {
            version = Version,
            name = Name,
            description = Description,
            collections = Collections.Select(CreateCollectionChecksumPayload)
        };

        var json = JsonSerializer.Serialize(payload);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));

        return Convert.ToHexString(hash);
    }

    private static object CreateCollectionChecksumPayload(MongoCollectionDefinition collection)
    {
        var indexes = collection.Indexes.Select(index => new
        {
            name = index.Name,
            fields = index.Fields.Select(field => new
            {
                name = field.Name,
                direction = field.Direction
            }),
            unique = index.Unique,
            expireAfterSeconds = index.ExpireAfterSeconds
        });

        // M01 no declara validador. Se conserva exactamente la forma anterior para
        // no alterar su checksum publicado; el validador solo aparece en versiones
        // nuevas que lo declaren.
        return collection.Validator is null
            ? new
            {
                name = collection.Name,
                indexes
            }
            : new
            {
                name = collection.Name,
                validator = new
                {
                    filter = collection.Validator.CanonicalJson,
                    validationLevel = collection.Validator.ValidationLevel,
                    validationAction = collection.Validator.ValidationAction
                },
                indexes
            };
    }
}
