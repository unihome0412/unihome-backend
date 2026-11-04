using System.Reflection;
using System.Xml.Linq;
using UniHome.Application.Abstractions.Storage;

namespace UniHome.UnitTests.Architecture;

public sealed class ArchitectureTests
{
    [Fact]
    public void Domain_NoDebeReferenciarDependenciasExternas()
    {
        var domainAssembly = Assembly.Load("UniHome.Domain");

        var forbiddenReferences = domainAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(IsForbiddenReference)
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    [Fact]
    public void Application_ProyectoDebeReferenciarSoloDomainEntreProyectosUniHome()
    {
        var projectPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UniHome.Application",
            "UniHome.Application.csproj");

        var project = XDocument.Load(projectPath);

        var projectReferences = project
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFileNameWithoutExtension(path!.Replace('\\', '/')))
            .ToArray();

        Assert.Equal(new[] { "UniHome.Domain" }, projectReferences);
        Assert.Empty(project.Descendants("PackageReference"));
    }

    [Fact]
    public void Application_NoDebeReferenciarInfrastructureNiFrameworksDeTransporte()
    {
        var applicationAssembly = typeof(StorageReadiness).Assembly;

        var forbiddenReferences = applicationAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(IsForbiddenReference)
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    private static bool IsForbiddenReference(string assemblyName) =>
        assemblyName.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase) ||
        assemblyName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) ||
        assemblyName.StartsWith("MongoDB.", StringComparison.OrdinalIgnoreCase) ||
        assemblyName.Equals("UniHome.Infrastructure", StringComparison.OrdinalIgnoreCase) ||
        assemblyName.Equals("UniHome.Api", StringComparison.OrdinalIgnoreCase);

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
