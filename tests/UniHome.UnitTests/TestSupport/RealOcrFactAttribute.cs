namespace UniHome.UnitTests.TestSupport;

/// <summary>
/// Prueba real de OCR que solo se ejecuta si hay un binario Tesseract local.
/// En CI sin motor se omite explícitamente; no se simula el resultado.
/// </summary>
public sealed class RealOcrFactAttribute : FactAttribute
{
    public RealOcrFactAttribute()
    {
        if (ResolveExecutable() is null)
        {
            Skip = "Tesseract no está instalado. Configure UNIHOME_TESSERACT_PATH o instale el motor para la prueba real.";
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
