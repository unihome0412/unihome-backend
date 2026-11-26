using System.Reflection;

namespace UniHome.Api.Configuration;

/// <summary>
/// Versión de la API (Bloque 2 de la auditoría), tomada de
/// <c>&lt;Version&gt;</c> en <c>Directory.Build.props</c> vía el atributo
/// que el SDK de .NET genera automáticamente al compilar
/// (<see cref="AssemblyInformationalVersionAttribute"/>). No se guarda un
/// valor duplicado a mano en código: una sola fuente de verdad.
/// </summary>
internal static class ApiVersionInfo
{
    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.0.0-desconocida";
        }

        // En builds deterministas el SDK puede anexar "+<sha>" al valor de
        // <Version>; se conserva solo la parte semántica para la cabecera y
        // el health check.
        var plusIndex = informational.IndexOf('+');
        return plusIndex >= 0 ? informational[..plusIndex] : informational;
    }
}
