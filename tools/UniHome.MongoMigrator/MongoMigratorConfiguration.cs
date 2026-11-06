using System.Globalization;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using UniHome.Infrastructure.Configuration;

namespace UniHome.MongoMigrator;

public static class MongoMigratorConfiguration
{
    public const string PromotionUserSecretsId = "unihome-backend-migrations";
    public const string PromotionConnectionStringKey = "MongoDb:MigrationConnectionString";

    // AddEnvironmentVariables normaliza __ a :. Se conserva el fallback literal
    // anterior por compatibilidad con otros proveedores, sin cambiar credenciales.
    public static string GetConnectionString(IConfiguration configuration)
    {
        var value = configuration["MongoDb:ConnectionString"];
        if (string.IsNullOrWhiteSpace(value))
        {
            value = configuration["MongoDb__ConnectionString"];
        }
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Configuración MongoDB incompleta.");
        }
        return value.Trim();
    }

    /// <summary>
    /// Credencial exclusiva de promoción. No usa MongoDb:ConnectionString ni su
    /// equivalente de entorno como respaldo. Si coincide con la credencial
    /// operativa configurada, se rechaza por posible uso accidental.
    /// </summary>
    public static string GetPromotionConnectionString(IConfiguration configuration)
    {
        var value = configuration[PromotionConnectionStringKey];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "La credencial exclusiva de migración no está configurada.");
        }

        var migrationConnectionString = value.Trim();
        var operationalConnectionString = GetOptionalConnectionString(configuration);

        if (!string.IsNullOrWhiteSpace(operationalConnectionString) &&
            HaveSameMongoIdentity(migrationConnectionString, operationalConnectionString))
        {
            throw new InvalidOperationException(
                "La credencial de migración no puede ser la misma que la operativa.");
        }

        return migrationConnectionString;
    }

    private static bool HaveSameMongoIdentity(string first, string second)
    {
        if (string.Equals(first, second, StringComparison.Ordinal))
        {
            return true;
        }

        MongoClientSettings firstSettings;
        MongoClientSettings secondSettings;

        try
        {
            firstSettings = MongoClientSettings.FromConnectionString(first);
            secondSettings = MongoClientSettings.FromConnectionString(second);
        }
        catch (Exception)
        {
            // La validación de formato definitiva ocurre al construir el cliente.
            // Si no se puede comparar, no se afirma que las identidades sean iguales.
            return false;
        }

        return HaveSameCredential(firstSettings.Credential, secondSettings.Credential) &&
               string.Equals(
                   firstSettings.ReplicaSetName,
                   secondSettings.ReplicaSetName,
                   StringComparison.Ordinal) &&
               firstSettings.DirectConnection == secondSettings.DirectConnection &&
               HaveSameServers(firstSettings.Servers, secondSettings.Servers) &&
               string.Equals(
                   GetDatabasePath(first),
                   GetDatabasePath(second),
                   StringComparison.Ordinal);
    }

    private static bool HaveSameCredential(
        MongoCredential? first,
        MongoCredential? second)
    {
        if (first is null || second is null)
        {
            return first is null && second is null;
        }

        return string.Equals(first.Username, second.Username, StringComparison.Ordinal) &&
               string.Equals(first.Source, second.Source, StringComparison.Ordinal) &&
               string.Equals(first.Mechanism, second.Mechanism, StringComparison.Ordinal);
    }

    private static bool HaveSameServers(
        IEnumerable<MongoServerAddress> first,
        IEnumerable<MongoServerAddress> second)
    {
        static string[] Canonicalize(IEnumerable<MongoServerAddress> servers) =>
            servers
                .Select(server =>
                    $"{server.Host.ToUpperInvariant()}:{server.Port.ToString(CultureInfo.InvariantCulture)}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

        return Canonicalize(first).SequenceEqual(Canonicalize(second), StringComparer.Ordinal);
    }

    private static string GetDatabasePath(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        return Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
    }

    private static string? GetOptionalConnectionString(IConfiguration configuration)
    {
        var value = configuration["MongoDb:ConnectionString"];
        if (string.IsNullOrWhiteSpace(value))
        {
            value = configuration["MongoDb__ConnectionString"];
        }
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static int GetTimeoutSeconds(IConfiguration configuration)
    {
        var raw = configuration["MongoDb:ServerSelectionTimeoutSeconds"] ??
                  configuration["MongoDb__ServerSelectionTimeoutSeconds"];
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) &&
               seconds is > 0 and <= 60
            ? seconds
            : MongoDbOptions.DefaultServerSelectionTimeoutSeconds;
    }
}
