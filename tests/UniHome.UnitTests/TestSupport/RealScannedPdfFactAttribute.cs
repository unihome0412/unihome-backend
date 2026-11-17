namespace UniHome.UnitTests.TestSupport;

/// <summary>
/// Prueba real de PDF escaneado: exige Tesseract y Poppler locales. En CI se
/// instalan ambos; sin ellos se omite explícitamente.
/// </summary>
public sealed class RealScannedPdfFactAttribute : FactAttribute
{
    public RealScannedPdfFactAttribute()
    {
        if (RealOcrFactAttribute.ResolveExecutable() is null ||
            ResolvePdftoppm() is null)
        {
            Skip = "Requiere Tesseract y pdftoppm (Poppler) locales para la prueba real de PDF escaneado.";
        }
    }

    public static string? ResolvePdftoppm()
    {
        var configured = Environment.GetEnvironmentVariable("UNIHOME_PDFTOPPM_PATH");

        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var wingetPackages = Path.Combine(
            localAppData,
            "Microsoft",
            "WinGet",
            "Packages");

        if (Directory.Exists(wingetPackages))
        {
            foreach (var directory in Directory.EnumerateDirectories(
                         wingetPackages,
                         "*Poppler*",
                         SearchOption.TopDirectoryOnly))
            {
                var candidate = Directory
                    .EnumerateFiles(directory, "pdftoppm.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (candidate is not null)
                {
                    return candidate;
                }
            }
        }

        return UniHome.Infrastructure.Analysis.LocalExecutableResolver.Resolve("pdftoppm");
    }
}
