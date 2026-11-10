using PactNet;
using PactNet.Verifier;

namespace UniHome.PactTests;

/// <summary>
/// Verificación de contratos Pact reales del consumidor Android. El proveedor
/// debe ser una API UniHome en ejecución (Kestrel, entorno aislado) indicada por
/// <c>PACT_PROVIDER_BASE_URL</c>; PactNet no puede verificar TestServer.
/// </summary>
public sealed class ProviderContractTests
{
    private const string ProviderName = "UniHome API";

    [Fact]
    public void Provider_DebeCumplirLosContratosPactRealesDelConsumidor()
    {
        var contractsDirectory = Environment.GetEnvironmentVariable("PACT_CONTRACTS_DIR")
            ?? "pacts";
        var directory = Path.GetFullPath(contractsDirectory);
        var files = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            : [];

        if (files.Length == 0)
        {
            throw new InvalidOperationException(
                $"No hay contratos Pact reales en '{directory}'. El job debe permanecer " +
                "desactivado hasta que el consumidor Android entregue archivos Pact " +
                "generados por sus pruebas de cliente; no se generan desde OpenAPI.");
        }

        var baseUrl = Environment.GetEnvironmentVariable("PACT_PROVIDER_BASE_URL")
            ?? "http://127.0.0.1:5088";
        var config = new PactVerifierConfig();
        using var verifier = new PactVerifier(ProviderName, config);
        var source = verifier
            .WithHttpEndpoint(new Uri(baseUrl))
            .WithDirectorySource(new DirectoryInfo(directory));

        var providerStateUrl = Environment.GetEnvironmentVariable("PACT_PROVIDER_STATE_URL");
        if (!string.IsNullOrWhiteSpace(providerStateUrl))
        {
            source.WithProviderStateUrl(new Uri(providerStateUrl));
        }

        source.Verify();
    }
}
