using MongoDB.Driver;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Configuración exclusiva de pruebas reales contra MongoDB. La prueba se omite
/// salvo que se habilite explícitamente y la base sea una base de verificación
/// autorizada; nunca acepta unihome_desarrollo.
/// </summary>
internal static class MongoIntegrationEnvironment
{
    public const string RunVariable = "UNIHOME_RUN_MONGO_INTEGRATION_TESTS";

    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("MongoDb__ConnectionString");

    public static string? DatabaseName =>
        Environment.GetEnvironmentVariable("MongoDb__DatabaseName");

    public static bool IsConfigured =>
        string.Equals(Environment.GetEnvironmentVariable(RunVariable), "1", StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(ConnectionString) &&
        MongoMigrationGuard.IsAllowed(
            DatabaseName,
            Environment.GetEnvironmentVariable(MongoMigrationGuard.AllowlistEnvironmentVariable));

    public static string SkipReason =>
        $"Defina {RunVariable}=1, MongoDb__ConnectionString y MongoDb__DatabaseName " +
        $"({MongoMigrationGuard.TestDatabaseName} o base autorizada) para ejecutar la prueba real.";

    public static MongoClient CreateClient()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(SkipReason);
        }

        return CreateClientFromConnectionString(ConnectionString!);
    }

    internal static MongoClient CreateClientFromConnectionString(string connectionString)
    {
        try
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString.Trim());
            settings.ApplicationName = "UniHome.IntegrationTests";
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(15);
            return new MongoClient(settings);
        }
        catch (Exception)
        {
            // xUnit imprime excepciones en sus resultados: nunca conservar ni el
            // mensaje del parser ni una InnerException que contenga una URI.
            throw new InvalidOperationException("Configuración MongoDB de pruebas no válida; revisa el entorno seguro.");
        }
    }

    public static IMongoDatabase GetDatabase(MongoClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.GetDatabase(DatabaseName!.Trim());
    }
}
