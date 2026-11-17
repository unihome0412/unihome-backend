using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UniHome.Infrastructure.Analysis;
using UniHome.Infrastructure.Configuration;

namespace UniHome.UnitTests.Infrastructure;

public sealed class TesseractCliOcrEngineTests
{
    [Fact]
    public async Task ProcesoQueTerminaConError_DebeMarcarFallidoSinTexto()
    {
        var executable = OperatingSystem.IsWindows()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "findstr.exe")
            : "/bin/false";
        Assert.True(File.Exists(executable));

        var options = new DocumentAnalysisOptions
        {
            TesseractExecutablePath = executable!,
            TesseractLanguage = "spa+eng",
            TesseractTimeoutSeconds = 30,
            TesseractPageSegmentationMode = 6
        };
        var engine = new TesseractCliOcrEngine(
            Options.Create(options),
            NullLogger<TesseractCliOcrEngine>.Instance);

        var result = await engine.ReadAsync(
            [0x89, 0x50, 0x4E, 0x47],
            "image/png",
            CancellationToken.None);

        Assert.True(result.Available);
        Assert.True(result.Failed);
        Assert.Null(result.Text);
        Assert.Contains("error", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EjecutableNoEjecutable_DebeMarcarFallidoControlado()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"unihome-no-exec-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "no es un ejecutable");

        try
        {
            var options = new DocumentAnalysisOptions
            {
                TesseractExecutablePath = path
            };
            var engine = new TesseractCliOcrEngine(
                Options.Create(options),
                NullLogger<TesseractCliOcrEngine>.Instance);

            var result = await engine.ReadAsync(
                [1, 2, 3],
                "image/jpeg",
                CancellationToken.None);

            Assert.True(result.Available);
            Assert.True(result.Failed);
            Assert.Null(result.Text);
            Assert.Contains("procesar", result.Detail, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CancelacionPrevia_DebePropagarSinConvertirlaEnResultado()
    {
        var executable = OperatingSystem.IsWindows()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "findstr.exe")
            : "/bin/false";
        var options = new DocumentAnalysisOptions
        {
            TesseractExecutablePath = executable
        };
        var engine = new TesseractCliOcrEngine(
            Options.Create(options),
            NullLogger<TesseractCliOcrEngine>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.ReadAsync([1, 2, 3], "image/png", cancellation.Token));
    }

    [Fact]
    public void ResolucionDeEjecutable_DebeDistinguirRutaValidaDeAusente()
    {
        var missing = Path.Combine(
            Path.GetTempPath(),
            $"unihome-missing-{Guid.NewGuid():N}.exe");

        Assert.Null(TesseractCliOcrEngine.ResolveExecutable(""));
        Assert.Null(TesseractCliOcrEngine.ResolveExecutable("   "));
        Assert.Null(TesseractCliOcrEngine.ResolveExecutable("unihome-no-existe"));
        Assert.Null(TesseractCliOcrEngine.ResolveExecutable(missing));
        Assert.Equal(
            Environment.ProcessPath,
            TesseractCliOcrEngine.ResolveExecutable(Environment.ProcessPath!));
    }
}
