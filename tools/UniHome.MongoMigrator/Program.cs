using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.MongoMigrator;
using UniHome.MongoMigrator.Diagnostics;

// Herramienta manual de migración. No se ejecuta desde la API ni durante dotnet test.
// Nunca imprime la cadena de conexión, documentos, correos, códigos ni tokens.

const string Usage =
    """
    Uso:
      dotnet run --project tools/UniHome.MongoMigrator -- audit --database <nombre>
      dotnet run --project tools/UniHome.MongoMigrator -- plan --database <nombre>
      dotnet run --project tools/UniHome.MongoMigrator -- apply --database <nombre> --confirm
      dotnet run --project tools/UniHome.MongoMigrator -- verify --database <nombre>
      dotnet run --project tools/UniHome.MongoMigrator -- promote --database unihome_desarrollo --confirm-database unihome_desarrollo --approval-ref <referencia>

    audit, plan, apply y verify aceptan --version <M01-001|M03-001>; el valor
    predeterminado es M01-001. audit, plan y verify aceptan --migration-credential para leer con la credencial
    técnica. La migración ordinaria solo escribe en unihome_modelo_pruebas o en una
    base incluida en UNIHOME_MIGRATION_ALLOWED_DATABASES. unihome_desarrollo solo
    puede promoverse con el comando promote, autorización específica y credencial
    técnica. La cadena de conexión se toma de User Secrets o de variables de entorno
    y nunca se imprime. Para promote se usa exclusivamente
    MongoDb:MigrationConnectionString.
    """;

if (args.Length == 0 || IsHelp(args[0]))
{
    Console.WriteLine(Usage);
    return 0;
}

var command = args[0].Trim().ToLowerInvariant();
var databaseName = GetOption(args, "--database");
var stage = MongoExecutionStage.ConfigurationValidation;
var diagnostics = new SafeMongoDiagnostics();
MongoClient? client = null;
var exitCode = 2;
MongoPromotionAuthorization? promotionAuthorization = null;

try
{
    if (string.IsNullOrWhiteSpace(databaseName) ||
        command is not ("audit" or "plan" or "apply" or "verify" or "promote"))
    {
        throw new ArgumentException("Comando o base de datos no válidos.");
    }

    var useMigrationCredential = command == "promote" ||
        args.Contains("--migration-credential", StringComparer.OrdinalIgnoreCase);

    if (command == "apply" && useMigrationCredential)
    {
        throw new InvalidOperationException(
            "apply no usa la credencial ni la ruta de promoción.");
    }

    if (command == "promote")
    {
        promotionAuthorization = MongoPromotionGuard.EnsureAuthorized(
            databaseName,
            GetOption(args, "--confirm-database"),
            Environment.GetEnvironmentVariable(
                MongoPromotionGuard.AuthorizationTargetEnvironmentVariable),
            GetOption(args, "--approval-ref"));

        var version = GetOption(args, "--version");
        if (!string.IsNullOrWhiteSpace(version))
        {
            promotionAuthorization = promotionAuthorization with { Version = version };
        }
    }

    stage = MongoExecutionStage.ConfigurationLoading;
    IConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
    if (useMigrationCredential)
    {
        configurationBuilder.AddUserSecrets(
            MongoMigratorConfiguration.PromotionUserSecretsId,
            true);
    }
    else
    {
        configurationBuilder.AddUserSecrets(typeof(Program).Assembly, optional: true);
    }
    var configuration = configurationBuilder
        .AddEnvironmentVariables()
        .Build();

    stage = MongoExecutionStage.ConfigurationValidation;
    var connectionString = useMigrationCredential
        ? MongoMigratorConfiguration.GetPromotionConnectionString(configuration)
        : MongoMigratorConfiguration.GetConnectionString(configuration);
    var timeoutSeconds = MongoMigratorConfiguration.GetTimeoutSeconds(configuration);

    stage = MongoExecutionStage.SettingsParsing;
    var settings = MongoClientSettings.FromConnectionString(connectionString);
    settings.ApplicationName = "UniHome.Migrations";
    settings.ServerSelectionTimeout = TimeSpan.FromSeconds(timeoutSeconds);
    settings.ClusterConfigurator = cluster =>
    {
        // No inspeccionar endpoints, mensajes del driver o descripciones SDAM.
        cluster.Subscribe<ServerHeartbeatFailedEvent>(failure =>
            diagnostics.ObserveTransportFailure(failure.Exception, MongoExecutionStage.ServerSelection));
        cluster.Subscribe<ConnectionOpeningFailedEvent>(failure =>
            diagnostics.ObserveTransportFailure(failure.Exception, MongoExecutionStage.ConnectionOpening));
    };

    if (useMigrationCredential)
    {
        MongoPromotionTlsGuard.EnsureSecure(connectionString, settings);
    }

    stage = MongoExecutionStage.ClientConstruction;
    client = new MongoClient(settings);
    stage = MongoExecutionStage.DatabaseSelection;
    var selectedDatabase = command == "promote" ? databaseName : databaseName.Trim();
    var database = client.GetDatabase(selectedDatabase);
    var versionOption = GetOption(args, "--version");
    var selectedMigration = string.IsNullOrWhiteSpace(versionOption)
        ? MongoIdentityMigration.Definition
        : MongoMigrationRegistry.Default.GetRequired(versionOption);
    var runner = new MongoMigrationRunner(database, selectedMigration);

    stage = command == "promote"
        ? MongoExecutionStage.Promotion
        : MongoExecutionStage.MongoCommandExecution;
    exitCode = command switch
    {
        "audit" => await AuditAsync(runner),
        "plan" => await PlanAsync(runner),
        "apply" => await ApplyAsync(runner, args.Contains("--confirm", StringComparer.OrdinalIgnoreCase)),
        "verify" => await VerifyAsync(runner),
        "promote" => await PromoteAsync(database, promotionAuthorization!),
        _ => throw new InvalidOperationException("Comando no válido.")
    };
}
catch (Exception exception)
{
    diagnostics.WriteFailure(Console.Error, exception, stage);
    exitCode = exception switch
    {
        MongoMigrationDivergenceException => 3,
        InvalidOperationException => 4,
        MongoException => 5,
        _ => 2
    };
}
finally
{
    // Dispose no debe sustituir ni ocultar la excepción del comando.
    if (!diagnostics.TryDispose(client, Console.Error) && exitCode == 0)
    {
        exitCode = 2;
    }
}
return exitCode;

static bool IsHelp(string value) =>
    value is "-h" or "--help" or "help" ||
    string.Equals(value, "/?", StringComparison.Ordinal);

static string? GetOption(string[] args, string name)
{
    for (var index = 0; index < args.Length; index++)
    {
        if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
        {
            return index + 1 < args.Length ? args[index + 1] : null;
        }

        var prefix = name + "=";

        if (args[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return args[index][prefix.Length..];
        }
    }

    return null;
}

static async Task<int> AuditAsync(MongoMigrationRunner runner)
{
    var audit = await runner.AuditAsync();
    var managedCollections = MongoMigrationRegistry.Default.ManagedCollections;
    var externalCollections = audit.Collections
        .Where(collection => !managedCollections.Contains(collection.Name))
        .Where(collection => !string.Equals(
            collection.Name,
            MongoIdentitySchema.MigrationLedgerCollection,
            StringComparison.Ordinal))
        .ToArray();

    Console.WriteLine($"Base: {audit.DatabaseName}");
    Console.WriteLine($"Colecciones: {audit.Collections.Count}");
    Console.WriteLine();

    foreach (var collection in audit.Collections.OrderBy(item => item.Name, StringComparer.Ordinal))
    {
        var validator = collection.HasValidator
            ? $"{collection.ValidationLevel}/{collection.ValidationAction}"
            : "sin validador";

        Console.WriteLine(
            $"- {collection.Name} | documentos~{collection.EstimatedDocumentCount} | " +
            $"validador={validator}");

        foreach (var index in collection.Indexes.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            var ttl = index.ExpireAfterSeconds.HasValue
                ? $" ttl={index.ExpireAfterSeconds.Value}s"
                : string.Empty;
            var unique = index.Unique ? " unique" : string.Empty;

            Console.WriteLine($"    · {index.Name} {index.Key}{unique}{ttl}");
        }
    }

    Console.WriteLine();
    Console.WriteLine(
        $"Validadores strict/error (excluye M01 y migraciones): " +
        $"{externalCollections.Count(collection => IsStrictError(collection))}/{externalCollections.Length}");

    return externalCollections.All(IsStrictError) ? 0 : 6;

    static bool IsStrictError(MongoCollectionAudit collection) =>
        collection.HasValidator &&
        string.Equals(collection.ValidationLevel, "strict", StringComparison.Ordinal) &&
        string.Equals(collection.ValidationAction, "error", StringComparison.Ordinal);
}

static async Task<int> PlanAsync(MongoMigrationRunner runner)
{
    var plan = await runner.PlanAsync();

    Console.WriteLine($"Base: {plan.DatabaseName}");
    Console.WriteLine($"Migración: {plan.Version}");
    PrintWrites(plan.Writes);

    return 0;
}

static async Task<int> ApplyAsync(MongoMigrationRunner runner, bool confirmed)
{
    if (!confirmed)
    {
        Console.Error.WriteLine(
            "La aplicación de DDL requiere --confirm después de revisar el resultado de plan.");
        return 2;
    }

    MongoMigrationGuard.EnsureAllowed(runner.DatabaseName,
        Environment.GetEnvironmentVariable(MongoMigrationGuard.AllowlistEnvironmentVariable));
    Console.WriteLine($"Base autorizada: {runner.DatabaseName}");

    var plan = await runner.PlanAsync();
    PrintWrites(plan.Writes);

    if (!plan.RequiresWrites)
    {
        Console.WriteLine("Sin cambios pendientes.");
        return 0;
    }

    await runner.ApplyAsync();
    Console.WriteLine("Migración aplicada; las escrituras son idempotentes.");

    var verification = await runner.VerifyAsync();
    return PrintVerification(verification) ? 0 : 3;
}

static async Task<int> VerifyAsync(MongoMigrationRunner runner)
{
    var verification = await runner.VerifyAsync();
    return PrintVerification(verification) ? 0 : 3;
}

static async Task<int> PromoteAsync(
    IMongoDatabase database,
    MongoPromotionAuthorization authorization)
{
    var service = MongoPromotionService.Create(database, MongoMigrationRegistry.Default);
    var result = await service.PromoteAsync(authorization);

    Console.WriteLine($"Base: {result.FinalPreflight.DatabaseName}");
    Console.WriteLine($"Migración: {result.FinalPreflight.Version}");
    Console.WriteLine($"Estado inicial: {result.InitialPreflight.State}");
    Console.WriteLine(
        $"Escrituras planificadas: {result.InitialPreflight.PendingWrites.Count}");
    Console.WriteLine($"Estado con bloqueo: {result.FinalPreflight.State}");
    Console.WriteLine($"Escrituras aplicadas: {result.WritesApplied}");
    Console.WriteLine($"Verificación: {(result.Verification.IsValid ? "VÁLIDA" : "NO VÁLIDA")}");

    if (result.IsNoOp)
    {
        Console.WriteLine("Sin cambios pendientes; la migración ya estaba aplicada.");
    }

    return 0;
}

static void PrintWrites(IReadOnlyList<MongoMigrationWrite> writes)
{
    if (writes.Count == 0)
    {
        Console.WriteLine("Escrituras pendientes: 0");
        return;
    }

    Console.WriteLine($"Escrituras pendientes: {writes.Count}");

    foreach (var write in writes)
    {
        var detail = string.IsNullOrWhiteSpace(write.Detail) ? string.Empty : $" | {write.Detail}";
        Console.WriteLine($"- {write.Kind}: {write.Collection}/{write.Name}{detail}");
    }
}

static bool PrintVerification(MongoMigrationVerification verification)
{
    Console.WriteLine($"Base: {verification.DatabaseName}");
    Console.WriteLine($"Migración: {verification.Version}");
    Console.WriteLine($"Registrada: {verification.Applied}");
    Console.WriteLine($"Checksum coincide: {verification.ChecksumMatches}");
    var indexLabel = string.Equals(
        verification.Version,
        MongoIdentityMigration.Version,
        StringComparison.Ordinal)
        ? "M01"
        : verification.Version;
    Console.WriteLine($"Índices {indexLabel} coinciden: {verification.IndexesMatch}");
    Console.WriteLine($"Escrituras pendientes: {verification.PendingWrites.Count}");

    foreach (var issue in verification.Issues)
    {
        Console.WriteLine($"Problema: {issue}");
    }

    var managedCollections = MongoMigrationRegistry.Default.ManagedCollections;
    var externalCollections = verification.Collections
        .Where(collection => !managedCollections.Contains(collection.Name))
        .Where(collection => !string.Equals(
            collection.Name,
            MongoIdentitySchema.MigrationLedgerCollection,
            StringComparison.Ordinal))
        .ToArray();

    var strictError = externalCollections.Count(collection =>
        collection.HasValidator &&
        string.Equals(collection.ValidationLevel, "strict", StringComparison.Ordinal) &&
        string.Equals(collection.ValidationAction, "error", StringComparison.Ordinal));

    Console.WriteLine(
        $"Validadores strict/error (excluye M01 y migraciones): " +
        $"{strictError}/{externalCollections.Length}");
    var valid = verification.IsValid && strictError == externalCollections.Length;
    Console.WriteLine($"Resultado: {(valid ? "VÁLIDO" : "NO VÁLIDO")}");
    return valid;
}
