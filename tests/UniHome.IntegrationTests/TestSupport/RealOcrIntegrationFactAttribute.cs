namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Prueba end-to-end real que solo corre cuando Tesseract está disponible. Si no
/// existe el motor, se omite y no se declara validada la extracción.
/// </summary>
public sealed class RealOcrIntegrationFactAttribute : FactAttribute
{
    public RealOcrIntegrationFactAttribute()
    {
        if (ResolveExecutable() is null)
        {
            Skip = "Tesseract no está instalado. Configure UNIHOME_TESSERACT_PATH para la prueba real.";
        }
    }

    public static string? ResolveExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("UNIHOME_TESSERACT_PATH");

        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidate = Path.Combine(localAppData, "Programs", "Tesseract-OCR", "tesseract.exe");

        if (File.Exists(candidate))
        {
            return candidate;
        }

        return UniHome.Infrastructure.Analysis.TesseractCliOcrEngine.ResolveExecutable("tesseract");
    }
}
