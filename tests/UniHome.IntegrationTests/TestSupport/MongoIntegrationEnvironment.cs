using System.Net;
using MongoDB.Driver;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Configuración exclusiva de pruebas reales contra MongoDB. La prueba se omite
/// salvo que se habilite explícitamente y la base sea una base de verificación
/// autorizada; nunca acepta unihome_desarrollo. En CI se puede exigir además que
/// la conexión sea loopback, directa y sin credenciales.
/// </summary>
internal static class MongoIntegrationEnvironment
{
    public const string RunVariable = "UNIHOME_RUN_MONGO_INTEGRATION_TESTS";
    public const string LoopbackOnlyVariable = "UNIHOME_MONGO_CI_LOOPBACK_ONLY";

    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("MongoDb__ConnectionString");

    public static string? DatabaseName =>
        Environment.GetEnvironmentVariable("MongoDb__DatabaseName");

    public static bool LoopbackOnlyRequired =>
        string.Equals(
            Environment.GetEnvironmentVariable(LoopbackOnlyVariable),
            "1",
            StringComparison.Ordinal);

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

        if (LoopbackOnlyRequired)
        {
            EnsureLoopbackOnly(ConnectionString!);
        }

        return CreateClientFromConnectionString(ConnectionString!);
    }

    /// <summary>
    /// Defensa adicional para la base efímera de GitHub Actions: rechaza Atlas,
    /// SRV, credenciales y cualquier host que no sea loopback. Los mensajes no
    /// incluyen la URI para no filtrar datos sensibles en los resultados.
    /// </summary>
    internal static void EnsureLoopbackOnly(string connectionString)
    {
        var candidate = connectionString.Trim();
        if (!candidate.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "La conexión MongoDB de CI debe usar un esquema directo, sin SRV ni credenciales.");
        }

        MongoClientSettings settings;
        try
        {
            settings = MongoClientSettings.FromConnectionString(candidate);
        }
        catch (Exception)
        {
            throw new InvalidOperationException(
                "La conexión MongoDB de CI no tiene un formato local válido.");
        }

        if (settings.Credential is not null)
        {
            throw new InvalidOperationException(
                "La conexión MongoDB de CI no debe incluir credenciales.");
        }

        var servers = settings.Servers.ToArray();
        if (!settings.DirectConnection ||
            servers.Length != 1 ||
            !IsLoopbackHost(servers[0].Host))
        {
            throw new InvalidOperationException(
                "La conexión MongoDB de CI debe ser directa y apuntar solo a un host loopback.");
        }
    }

    private static bool IsLoopbackHost(string host)
    {
        var candidate = host.Trim().Trim('[', ']');
        return string.Equals(candidate, "localhost", StringComparison.OrdinalIgnoreCase) ||
               (IPAddress.TryParse(candidate, out var address) && IPAddress.IsLoopback(address));
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
