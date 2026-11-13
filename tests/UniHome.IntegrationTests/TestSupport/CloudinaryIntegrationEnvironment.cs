using System.Text.Json;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Prueba real opt-in de Cloudinary. Solo lee credenciales de variables de
/// entorno; nunca las imprime. Si se activa la ejecución y faltan credenciales,
/// la prueba debe fallar, no omitirse.
/// </summary>
internal static class CloudinaryIntegrationEnvironment
{
    public const string RunVariable = "UNIHOME_RUN_CLOUDINARY_INTEGRATION_TESTS";

    private const string SecretsId = "unihome-backend-api";
    private static readonly Lazy<Dictionary<string, string>> UserSecrets =
        new(LoadUserSecrets);

    public static string? CloudName => Read("Cloudinary__CloudName", "Cloudinary:CloudName");
    public static string? ApiKey => Read("Cloudinary__ApiKey", "Cloudinary:ApiKey");
    public static string? ApiSecret => Read("Cloudinary__ApiSecret", "Cloudinary:ApiSecret");

    private static string? Read(string environmentName, string secretName)
    {
        var value = Environment.GetEnvironmentVariable(environmentName);
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : UserSecrets.Value.TryGetValue(secretName, out var secret) &&
              !string.IsNullOrWhiteSpace(secret)
                ? secret
                : null;
    }

    private static Dictionary<string, string> LoadUserSecrets()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft",
                "UserSecrets",
                SecretsId,
                "secrets.json"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".microsoft",
                "usersecrets",
                SecretsId,
                "secrets.json")
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var values = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        values[property.Name] = property.Value.GetString() ?? string.Empty;
                    }
                }

                return values;
            }
            catch (Exception)
            {
                // No se registra el contenido ni la ruta de secretos.
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public static bool IsRunRequested =>
        string.Equals(
            Environment.GetEnvironmentVariable(RunVariable),
            "1",
            StringComparison.Ordinal);

    public static bool HasCredentials =>
        !string.IsNullOrWhiteSpace(CloudName) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ApiSecret);

    public static string SkipReason =>
        $"PENDIENTE: defina {RunVariable}=1, Cloudinary__CloudName, " +
        "Cloudinary__ApiKey y Cloudinary__ApiSecret en un entorno seguro.";

    public static CloudinaryIntegrationCredentials RequireConfiguration()
    {
        if (!IsRunRequested)
        {
            throw new InvalidOperationException(
                $"La prueba real Cloudinary no fue solicitada; defina {RunVariable}=1.");
        }

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(CloudName))
        {
            missing.Add("Cloudinary__CloudName");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            missing.Add("Cloudinary__ApiKey");
        }

        if (string.IsNullOrWhiteSpace(ApiSecret))
        {
            missing.Add("Cloudinary__ApiSecret");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"La prueba real Cloudinary está activada pero faltan: {string.Join(", ", missing)}.");
        }

        return new CloudinaryIntegrationCredentials(CloudName!, ApiKey!, ApiSecret!);
    }
}

internal sealed record CloudinaryIntegrationCredentials(
    string CloudName,
    string ApiKey,
    string ApiSecret);
