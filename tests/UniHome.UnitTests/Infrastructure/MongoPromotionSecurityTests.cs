using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.MongoMigrator;

namespace UniHome.UnitTests.Infrastructure;

/// <summary>
/// Pruebas de las puertas de seguridad de la promoción. No construyen un
/// MongoClient ni leen secretos reales.
/// </summary>
public sealed class MongoPromotionSecurityTests
{
    private const string ExpectedM01Checksum =
        "6FE8415A25A62BFA8C2A6D2F12C591B6645FF388D69E1B5260298B940E08CC4D";

    [Fact]
    public void M01_001_DebeConservarSuChecksumInmutable()
    {
        Assert.Equal(MongoIdentityMigration.Version, MongoIdentityMigration.Definition.Version);
        Assert.Equal(ExpectedM01Checksum, MongoIdentityMigration.Definition.Checksum);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unihome_modelo_pruebas")]
    [InlineData("unihome_otra")]
    [InlineData("UNIHOME_DESARROLLO")]
    [InlineData(" unihome_desarrollo")]
    [InlineData("unihome_desarrollo ")]
    [InlineData("admin")]
    public void Destino_NoAutorizado_DebeRechazarse(string? database)
    {
        Assert.Throws<MongoPromotionAuthorizationException>(() =>
            MongoPromotionGuard.EnsureAuthorized(
                database,
                MongoPromotionGuard.TargetDatabaseName,
                MongoPromotionGuard.TargetDatabaseName,
                "APR-001"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unihome_modelo_pruebas")]
    [InlineData("UNIHOME_DESARROLLO")]
    [InlineData(" unihome_desarrollo")]
    public void Confirmacion_Inexacta_DebeRechazarse(string? confirmation)
    {
        Assert.Throws<MongoPromotionAuthorizationException>(() =>
            MongoPromotionGuard.EnsureAuthorized(
                MongoPromotionGuard.TargetDatabaseName,
                confirmation,
                MongoPromotionGuard.TargetDatabaseName,
                "APR-001"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unihome_modelo_pruebas")]
    [InlineData("UNIHOME_DESARROLLO")]
    public void Autorizacion_Especifica_AusenteODeOtroDestino_DebeRechazarse(string? authorized)
    {
        Assert.Throws<MongoPromotionAuthorizationException>(() =>
            MongoPromotionGuard.EnsureAuthorized(
                MongoPromotionGuard.TargetDatabaseName,
                MongoPromotionGuard.TargetDatabaseName,
                authorized,
                "APR-001"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("linea\nsiguiente")]
    public void ReferenciaDeAprobacion_Invalida_DebeRechazarse(string? reference)
    {
        Assert.Throws<MongoPromotionAuthorizationException>(() =>
            MongoPromotionGuard.EnsureAuthorized(
                MongoPromotionGuard.TargetDatabaseName,
                MongoPromotionGuard.TargetDatabaseName,
                MongoPromotionGuard.TargetDatabaseName,
                reference));
    }

    [Fact]
    public void ReferenciaDeAprobacion_Excesiva_DebeRechazarse()
    {
        Assert.Throws<MongoPromotionAuthorizationException>(() =>
            MongoPromotionGuard.EnsureAuthorized(
                MongoPromotionGuard.TargetDatabaseName,
                MongoPromotionGuard.TargetDatabaseName,
                MongoPromotionGuard.TargetDatabaseName,
                new string('A', MongoPromotionGuard.MaximumApprovalReferenceLength + 1)));
    }

    [Fact]
    public void Autorizacion_Exacta_DebeAceptarse()
    {
        var authorization = MongoPromotionGuard.EnsureAuthorized(
            MongoPromotionGuard.TargetDatabaseName,
            MongoPromotionGuard.TargetDatabaseName,
            MongoPromotionGuard.TargetDatabaseName,
            "APR-001");

        Assert.Equal(MongoPromotionGuard.TargetDatabaseName, authorization.DatabaseName);
        Assert.Equal(MongoPromotionGuard.TargetDatabaseName, authorization.ConfirmationDatabase);
        Assert.Equal(MongoPromotionGuard.TargetDatabaseName, authorization.AuthorizedDatabase);
        Assert.Equal(MongoIdentityMigration.Version, authorization.Version);
    }

    [Fact]
    public void DestinoVerificacion_ConAutorizacionExacta_DebeAceptarse()
    {
        var authorization = MongoPromotionGuard.EnsureAuthorized(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            MongoPromotionGuard.VerificationTargetDatabaseName,
            MongoPromotionGuard.VerificationTargetDatabaseName,
            "APR-VERIF-001");

        Assert.Equal(MongoPromotionGuard.VerificationTargetDatabaseName, authorization.DatabaseName);
        Assert.Equal(MongoPromotionGuard.VerificationTargetDatabaseName, authorization.ConfirmationDatabase);
        Assert.Equal(MongoPromotionGuard.VerificationTargetDatabaseName, authorization.AuthorizedDatabase);
    }

    [Theory]
    [InlineData("unihome_desarrollo")]
    [InlineData("unihome_modelo_pruebas")]
    public void DestinoVerificacion_ConAutorizacionDeOtroDestino_DebeRechazarse(string authorized)
    {
        Assert.Throws<MongoPromotionAuthorizationException>(() =>
            MongoPromotionGuard.EnsureAuthorized(
                MongoPromotionGuard.VerificationTargetDatabaseName,
                MongoPromotionGuard.VerificationTargetDatabaseName,
                authorized,
                "APR-VERIF-001"));
    }

    [Theory]
    [InlineData("unihome_desarrollo")]
    [InlineData("unihome_desarrollo,unihome_verificacion")]
    [InlineData("unihome_desarrollo , unihome_modelo_pruebas")]
    public void AllowlistOrdinaria_NoDebeHabilitarPromocion(string allowlist)
    {
        Assert.False(MongoMigrationGuard.IsAllowed(
            MongoPromotionGuard.TargetDatabaseName,
            allowlist));
        Assert.Throws<InvalidOperationException>(() =>
            MongoMigrationGuard.EnsureAllowed(
                MongoPromotionGuard.TargetDatabaseName,
                allowlist));
        Assert.Throws<MongoPromotionAuthorizationException>(() =>
            MongoPromotionGuard.EnsureAuthorized(
                MongoPromotionGuard.TargetDatabaseName,
                MongoPromotionGuard.TargetDatabaseName,
                authorizedDatabase: null,
                approvalReference: "APR-001"));
    }

    [Fact]
    public void CredencialExclusiva_Ausente_NoDebeCaerEnLaCredencialOperativa()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["MongoDb:ConnectionString"] = "mongodb+srv://operativa.invalid/"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MongoMigratorConfiguration.GetPromotionConnectionString(configuration));

        Assert.DoesNotContain("operativa.invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CredencialExclusiva_NoDebeSerIgualALaOperativa()
    {
        const string shared = "mongodb+srv://shared.invalid/";
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["MongoDb:ConnectionString"] = shared,
            ["MongoDb:MigrationConnectionString"] = shared
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MongoMigratorConfiguration.GetPromotionConnectionString(configuration));

        Assert.DoesNotContain("shared.invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CredencialExclusiva_MismaIdentidadConOpcionesDistintas_DebeRechazarse()
    {
        const string baseUri =
            "mongodb://migrador:clave-sintetica@cluster.example.invalid:27017/unihome_desarrollo?authSource=admin";
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["MongoDb:ConnectionString"] = $"{baseUri}&appName=api",
            ["MongoDb:MigrationConnectionString"] = $"{baseUri}&appName=migrator"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MongoMigratorConfiguration.GetPromotionConnectionString(configuration));

        Assert.DoesNotContain("cluster.example.invalid", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clave-sintetica", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CredencialExclusiva_ConUsuarioDistinto_DebeAceptarse()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["MongoDb:ConnectionString"] =
                "mongodb://operador:clave-sintetica@cluster.example.invalid:27017/unihome_desarrollo?authSource=admin",
            ["MongoDb:MigrationConnectionString"] =
                "mongodb://migrador:otra-clave-sintetica@cluster.example.invalid:27017/unihome_desarrollo?authSource=admin"
        });

        var migration = MongoMigratorConfiguration.GetPromotionConnectionString(configuration);

        Assert.Contains("migrador", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void CredencialExclusiva_DebeLeerseSinFallback()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["MongoDb:ConnectionString"] = "mongodb+srv://operativa.invalid/",
            ["MongoDb:MigrationConnectionString"] = "mongodb+srv://migracion.invalid/"
        });

        Assert.Equal(
            "mongodb+srv://migracion.invalid/",
            MongoMigratorConfiguration.GetPromotionConnectionString(configuration));
    }

    [Fact]
    public void ConfiguracionDePromocion_DebeUsarUserSecretsSeparado()
    {
        Assert.Equal("unihome-backend-migrations", MongoMigratorConfiguration.PromotionUserSecretsId);
        Assert.NotEqual("unihome-backend-api", MongoMigratorConfiguration.PromotionUserSecretsId);
    }

    [Theory]
    [InlineData("mongodb://localhost")]
    [InlineData("mongodb://localhost/?tls=false")]
    public void Tls_Deshabilitado_DebeRechazarse(string connectionString)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);

        Assert.Throws<MongoPromotionSecurityException>(() =>
            MongoPromotionTlsGuard.EnsureSecure(connectionString, settings));
    }

    [Theory]
    [InlineData("mongodb://localhost/?tls=true&tlsInsecure=true")]
    [InlineData("mongodb://localhost/?tls=true&tlsAllowInvalidCertificates=true")]
    [InlineData("mongodb://localhost/?tls=true&tlsAllowInvalidHostnames=true")]
    public void Tls_Inseguro_DebeRechazarse(string connectionString)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);

        Assert.Throws<MongoPromotionSecurityException>(() =>
            MongoPromotionTlsGuard.EnsureSecure(connectionString, settings));
    }

    [Fact]
    public void Tls_ConCallbackPersonalizado_DebeRechazarse()
    {
        var settings = MongoClientSettings.FromConnectionString(
            "mongodb://localhost/?tls=true");
        settings.SslSettings = new SslSettings
        {
            ServerCertificateValidationCallback = (_, _, _, _) => true
        };

        Assert.Throws<MongoPromotionSecurityException>(() =>
            MongoPromotionTlsGuard.EnsureSecure(settings));
    }

    [Fact]
    public void Tls_ConValidacion_DebeAceptarse()
    {
        const string connectionString = "mongodb://localhost/?tls=true";
        var settings = MongoClientSettings.FromConnectionString(connectionString);

        MongoPromotionTlsGuard.EnsureSecure(connectionString, settings);
    }

    [Fact]
    public void Validador_DebeCanonicalizarElFiltro()
    {
        var first = new MongoValidatorDefinition(BsonDocument.Parse(
            "{ required: ['correo'], properties: { correo: { bsonType: 'string' } } }"));
        var second = new MongoValidatorDefinition(BsonDocument.Parse(
            "{ properties: { correo: { bsonType: 'string' } }, required: ['correo'] }"));

        Assert.True(first.Matches(
            second.Filter,
            MongoValidatorDefinition.StrictLevel,
            MongoValidatorDefinition.ErrorAction));
        Assert.NotEqual(first.CanonicalJson, string.Empty);
    }

    [Theory]
    [InlineData("off", "error")]
    [InlineData("strict", "ignore")]
    [InlineData("moderate", "warn")]
    public void Validador_ConNivelOAccionNoSoportados_DebeRechazarse(
        string level,
        string action)
    {
        if (level == "moderate" && action == "warn")
        {
            _ = new MongoValidatorDefinition(
                BsonDocument.Parse("{ required: ['campo'] }"),
                level,
                action);
            return;
        }

        Assert.Throws<ArgumentException>(() =>
            new MongoValidatorDefinition(
                BsonDocument.Parse("{ required: ['campo'] }"),
                level,
                action));
    }

    [Fact]
    public void Validador_DebeGenerarOpcionesDeCreacionSinPerderElFiltro()
    {
        var validator = new MongoValidatorDefinition(
            BsonDocument.Parse("{ required: ['campo'] }"),
            MongoValidatorDefinition.ModerateLevel,
            MongoValidatorDefinition.WarnAction);

        var options = validator.ToCreateCollectionOptions();

        Assert.IsType<CreateCollectionOptions<BsonDocument>>(options);
        Assert.NotNull(options.Validator);
        Assert.Equal(DocumentValidationLevel.Moderate, options.ValidationLevel);
        Assert.Equal(DocumentValidationAction.Warn, options.ValidationAction);
    }

    [Fact]
    public void Validador_DebeCambiarElChecksumDeUnaMigracionNueva()
    {
        var withoutValidator = new MongoMigrationDefinition(
            "M99-001",
            "Prueba",
            "Prueba",
            [new MongoCollectionDefinition("coleccion_prueba", [])]);
        var withValidator = new MongoMigrationDefinition(
            "M99-001",
            "Prueba",
            "Prueba",
            [
                new MongoCollectionDefinition(
                    "coleccion_prueba",
                    [],
                    new MongoValidatorDefinition(
                        BsonDocument.Parse("{ required: ['campo'] }")))
            ]);

        Assert.NotEqual(withoutValidator.Checksum, withValidator.Checksum);
    }

    private static IConfiguration BuildConfiguration(
        IReadOnlyDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
