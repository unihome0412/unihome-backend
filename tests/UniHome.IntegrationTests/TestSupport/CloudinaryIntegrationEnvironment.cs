namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Prueba real opt-in de Cloudinary. Solo lee credenciales de variables de
/// entorno; nunca las imprime. Si se activa la ejecución y faltan credenciales,
/// la prueba debe fallar, no omitirse.
/// </summary>
internal static class CloudinaryIntegrationEnvironment
{
    public const string RunVariable = "UNIHOME_RUN_CLOUDINARY_INTEGRATION_TESTS";

    public static string? CloudName => Environment.GetEnvironmentVariable("Cloudinary__CloudName");
    public static string? ApiKey => Environment.GetEnvironmentVariable("Cloudinary__ApiKey");
    public static string? ApiSecret => Environment.GetEnvironmentVariable("Cloudinary__ApiSecret");

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
