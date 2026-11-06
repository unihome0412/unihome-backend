using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

/// <summary>
/// Validador JSON Schema declarativo de una colección. No se usa para M01: el
/// contrato de identidad no define validador. Se incorpora para que las futuras
/// migraciones puedan materializar y verificar los validadores aprobados.
/// </summary>
public sealed class MongoValidatorDefinition
{
    public const string StrictLevel = "strict";
    public const string ModerateLevel = "moderate";
    public const string ErrorAction = "error";
    public const string WarnAction = "warn";

    private readonly BsonDocument _filter;

    public MongoValidatorDefinition(
        BsonDocument filter,
        string validationLevel = StrictLevel,
        string validationAction = ErrorAction)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.ElementCount == 0)
        {
            throw new ArgumentException("El validador no puede estar vacío.", nameof(filter));
        }

        if (validationLevel is not (StrictLevel or ModerateLevel))
        {
            throw new ArgumentException("El nivel de validación no es válido.", nameof(validationLevel));
        }

        if (validationAction is not (ErrorAction or WarnAction))
        {
            throw new ArgumentException("La acción de validación no es válida.", nameof(validationAction));
        }

        _filter = filter.DeepClone().AsBsonDocument;
        ValidationLevel = validationLevel;
        ValidationAction = validationAction;
        CanonicalJson = MongoBsonCanonicalizer.ToCanonicalJson(_filter);
    }

    public BsonDocument Filter => _filter.DeepClone().AsBsonDocument;

    public string ValidationLevel { get; }

    public string ValidationAction { get; }

    internal string CanonicalJson { get; }

    public bool Matches(
        BsonDocument? existingFilter,
        string? existingLevel,
        string? existingAction)
    {
        if (existingFilter is null)
        {
            return false;
        }

        return string.Equals(
                   CanonicalJson,
                   MongoBsonCanonicalizer.ToCanonicalJson(existingFilter),
                   StringComparison.Ordinal) &&
               string.Equals(
                   ValidationLevel,
                   string.IsNullOrWhiteSpace(existingLevel) ? StrictLevel : existingLevel,
                   StringComparison.Ordinal) &&
               string.Equals(
                   ValidationAction,
                   string.IsNullOrWhiteSpace(existingAction) ? ErrorAction : existingAction,
                   StringComparison.Ordinal);
    }

    public CreateCollectionOptions<BsonDocument> ToCreateCollectionOptions() => new()
    {
        Validator = new BsonDocumentFilterDefinition<BsonDocument>(
            Filter.DeepClone().AsBsonDocument),
        ValidationLevel = Enum.Parse<DocumentValidationLevel>(ValidationLevel, ignoreCase: true),
        ValidationAction = Enum.Parse<DocumentValidationAction>(ValidationAction, ignoreCase: true)
    };
}

internal static class MongoBsonCanonicalizer
{
    public static string ToCanonicalJson(BsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Sort(document).ToJson(new JsonWriterSettings
        {
            OutputMode = JsonOutputMode.CanonicalExtendedJson
        });
    }

    private static BsonDocument Sort(BsonDocument document) =>
        new(document.Elements
            .OrderBy(element => element.Name, StringComparer.Ordinal)
            .Select(element => new BsonElement(element.Name, SortValue(element.Value))));

    private static BsonValue SortValue(BsonValue value) => value switch
    {
        BsonDocument document => Sort(document),
        BsonArray array => new BsonArray(array.Select(SortValue)),
        _ => value
    };
}
