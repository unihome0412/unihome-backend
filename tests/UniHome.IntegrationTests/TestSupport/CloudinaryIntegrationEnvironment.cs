namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Prueba real opt-in de Cloudinary. Solo lee credenciales de variables de
/// entorno; nunca las imprime ni las incluye en el nombre de la prueba.
/// </summary>
internal static class CloudinaryIntegrationEnvironment
{
    public const string RunVariable = "UNIHOME_RUN_CLOUDINARY_INTEGRATION_TESTS";

    public static string? CloudName => Environment.GetEnvironmentVariable("Cloudinary__CloudName");
    public static string? ApiKey => Environment.GetEnvironmentVariable("Cloudinary__ApiKey");
    public static string? ApiSecret => Environment.GetEnvironmentVariable("Cloudinary__ApiSecret");

    public static bool IsConfigured =>
        string.Equals(Environment.GetEnvironmentVariable(RunVariable), "1", StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(CloudName) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ApiSecret);

    public static string SkipReason =>
        $"Defina {RunVariable}=1, Cloudinary__CloudName, Cloudinary__ApiKey y " +
        "Cloudinary__ApiSecret en un entorno seguro para ejecutar la prueba real.";
}
