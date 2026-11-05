using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using UniHome.Infrastructure.Persistence.Mongo.Accounts;
using UniHome.Infrastructure.Persistence.Mongo.Sessions;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoIdentityBsonMappingTests
{
    private static readonly DateTime Timestamp = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private const string Identifier = "00112233445566778899aabbccddeeff";

    [Fact]
    public void Account_DebeConservarNombresYTiposBsonSinObjectId()
    {
        var document = new AccountDocument
        {
            Id = Identifier,
            CreatedAtUtc = Timestamp,
            PrivacyAcceptance = new PrivacyAcceptanceDocument { AcceptedAtUtc = Timestamp },
            EmailVerification = new VerificationCodeDocument
            {
                IssuedAtUtc = Timestamp,
                ExpiresAtUtc = Timestamp.AddMinutes(10)
            },
            VerificationResendAttemptsUtc = [Timestamp]
        }.ToBsonDocument();

        AssertTypes(document,
            ("_id", BsonType.String),
            ("nombreCompleto", BsonType.String),
            ("correoNormalizado", BsonType.String),
            ("fechaNacimiento", BsonType.String),
            ("genero", BsonType.String),
            ("hashContrasena", BsonType.String),
            ("aceptacionPrivacidad", BsonType.Document),
            ("estado", BsonType.String),
            ("correoConfirmado", BsonType.Boolean),
            ("creadoEnUtc", BsonType.DateTime),
            ("correoConfirmadoEnUtc", BsonType.Null),
            ("verificacionCorreo", BsonType.Document),
            ("intentosReenvioUtc", BsonType.Array));
        AssertTypes(document["aceptacionPrivacidad"].AsBsonDocument,
            ("version", BsonType.String), ("aceptadaEnUtc", BsonType.DateTime));
        AssertTypes(document["verificacionCorreo"].AsBsonDocument,
            ("hash", BsonType.String), ("emitidoEnUtc", BsonType.DateTime),
            ("expiraEnUtc", BsonType.DateTime), ("consumidoEnUtc", BsonType.Null));
        Assert.Equal(BsonType.DateTime, Assert.Single(document["intentosReenvioUtc"].AsBsonArray).BsonType);
        Assert.Equal(Identifier, document["_id"].AsString);
        Assert.Equal(Timestamp, BsonSerializer.Deserialize<AccountDocument>(document).CreatedAtUtc);
    }

    [Fact]
    public void RefreshSession_DebeConservarNombresYTiposBsonSinObjectId()
    {
        var document = new RefreshSessionDocument
        {
            Id = Identifier,
            AccountId = Identifier,
            CurrentTokenIssuedAtUtc = Timestamp,
            ExpiresAtUtc = Timestamp.AddDays(30),
            CreatedAtUtc = Timestamp,
            LastRotatedAtUtc = Timestamp,
            UsedTokens = [new UsedRefreshTokenDocument { UsedAtUtc = Timestamp }]
        }.ToBsonDocument();

        AssertTypes(document,
            ("_id", BsonType.String), ("cuentaId", BsonType.String),
            ("hashRefreshActual", BsonType.String), ("refreshEmitidoEnUtc", BsonType.DateTime),
            ("expiraEnUtc", BsonType.DateTime), ("tokensUsados", BsonType.Array),
            ("revocadaEnUtc", BsonType.Null), ("motivoRevocacion", BsonType.Null),
            ("creadaEnUtc", BsonType.DateTime), ("ultimaRotacionEnUtc", BsonType.DateTime));
        AssertTypes(Assert.Single(document["tokensUsados"].AsBsonArray).AsBsonDocument,
            ("hash", BsonType.String), ("usadoEnUtc", BsonType.DateTime));
        Assert.Equal(Identifier, document["_id"].AsString);
        Assert.Equal(Identifier, document["cuentaId"].AsString);
        var restored = BsonSerializer.Deserialize<RefreshSessionDocument>(document);
        Assert.Equal(Timestamp, restored.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, restored.CreatedAtUtc.Kind);
    }

    private static void AssertTypes(BsonDocument document, params (string Name, BsonType Type)[] expected)
    {
        Assert.Equal(expected.Select(field => field.Name).Order(), document.Names.Order());
        foreach (var (name, type) in expected)
        {
            Assert.Equal(type, document[name].BsonType);
        }
    }
}
