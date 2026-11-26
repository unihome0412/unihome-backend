using System.Net;
using System.Text.Json;

namespace UniHome.IntegrationTests;

/// <summary>
/// Evita que una operación nueva del catálogo OpenAPI quede sin su spec en
/// <c>specs/</c> (auditoría 2026-12-02, sección Backend).
/// </summary>
public sealed class SpecsCoverageTests : IClassFixture<SwaggerEnabledApiFactory>
{
    private readonly HttpClient _client;

    public SpecsCoverageTests(SwaggerEnabledApiFactory factory)
    {
        _client = factory.CreateAdminClient();
    }

    [Fact]
    public async Task CadaOperacionDelCatalogoOpenApiDebeTenerUnaSpecEnDocsSpecs()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        var methods = new[] { "get", "post", "put", "patch", "delete", "head", "options" };

        var specsRoot = Path.Combine(FindRepositoryRoot(), "specs");
        Assert.True(Directory.Exists(specsRoot), $"No existe el directorio de specs esperado: {specsRoot}");

        var specFiles = Directory
            .GetFiles(specsRoot, "*.md", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = new List<string>();

        foreach (var path in paths.EnumerateObject()
                     .Where(p => p.Name.StartsWith("/api/v1/", StringComparison.Ordinal)))
        {
            foreach (var method in methods.Where(m => path.Value.TryGetProperty(m, out _)))
            {
                var expectedFile = BuildSpecFileName(method, path.Name);
                if (!specFiles.Contains(expectedFile))
                {
                    missing.Add($"{method.ToUpperInvariant()} {path.Name} -> specs/.../{expectedFile}");
                }
            }
        }

        Assert.True(
            missing.Count == 0,
            "Operaciones del catálogo OpenAPI sin spec en specs/: " + string.Join("; ", missing));
    }

    private static string BuildSpecFileName(string method, string path)
    {
        const string prefix = "/api/v1/";
        var relative = path.StartsWith(prefix, StringComparison.Ordinal)
            ? path[prefix.Length..]
            : path;
        var slug = relative
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
            .Replace('/', '-');
        return $"{method.ToLowerInvariant()}-{slug}.md";
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UniHome.Backend.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("No se encontró la raíz de la solución.");
    }
}
