namespace UniHome.Infrastructure.Configuration;

/// <summary>
/// Análisis documental local. El OCR usa el ejecutable Tesseract si está
/// disponible; si falta, el resultado se marca NoDisponible y nunca se simula.
/// ELA y metadatos se ejecutan en proceso con ImageSharp.
/// </summary>
public sealed class DocumentAnalysisOptions
{
    public const string SectionName = "DocumentAnalysis";
    public const int MaxAllowedPixels = 40_000_000;

    public int MaxImagePixels { get; set; } = 20_000_000;

    public double ElaJpegQuality { get; set; } = 90;

    public double ElaMeanDifferenceThreshold { get; set; } = 8.0;

    public double ElaHighDifferenceRatioThreshold { get; set; } = 0.18;

    public int ElaHighDifferenceLevel { get; set; } = 24;

    public string TesseractExecutablePath { get; set; } = "tesseract";

    public string TesseractLanguage { get; set; } = "eng";

    public int TesseractTimeoutSeconds { get; set; } = 30;

    public int TesseractPageSegmentationMode { get; set; } = 6;

    public bool IsValid() =>
        MaxImagePixels is > 0 and <= MaxAllowedPixels &&
        ElaJpegQuality is >= 50 and <= 100 &&
        ElaMeanDifferenceThreshold is >= 0 and <= 255 &&
        ElaHighDifferenceRatioThreshold is >= 0 and <= 1 &&
        ElaHighDifferenceLevel is >= 1 and <= 255 &&
        TesseractTimeoutSeconds is >= 5 and <= 120 &&
        TesseractPageSegmentationMode is >= 0 and <= 13 &&
        !string.IsNullOrWhiteSpace(TesseractExecutablePath) &&
        !string.IsNullOrWhiteSpace(TesseractLanguage);
}
