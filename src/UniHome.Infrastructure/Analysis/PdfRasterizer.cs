using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Analysis;

internal sealed record PdfRasterization(
    IReadOnlyList<byte[]> Pages,
    bool Available,
    bool Failed,
    string Detail);

/// <summary>
/// Rasteriza PDF con Poppler (`pdftoppm`) para OCR y miniaturas. Es una herramienta
/// local gratuita; no se ejecuta contenido activo ni se usa un servicio externo.
/// Si el binario no existe, el resultado se marca NoDisponible.
/// </summary>
internal sealed class PdfRasterizer
{
    private readonly DocumentAnalysisOptions _options;
    private readonly ILogger<PdfRasterizer> _logger;

    public PdfRasterizer(
        IOptions<DocumentAnalysisOptions> options,
        ILogger<PdfRasterizer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PdfRasterization> RasterizeAsync(
        byte[] content,
        CancellationToken cancellationToken)
    {
        var executable = LocalExecutableResolver.Resolve(_options.PdftoppmExecutablePath);

        if (executable is null)
        {
            return new PdfRasterization(
                [],
                Available: false,
                Failed: false,
                "El rasterizador PDF local (pdftoppm) no está instalado.");
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "unihome-pdf",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var inputPath = Path.Combine(directory, "documento.pdf");
        var outputPrefix = Path.Combine(directory, "pagina");

        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(inputPath, content, cancellationToken).ConfigureAwait(false);

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-png");
            startInfo.ArgumentList.Add("-r");
            startInfo.ArgumentList.Add(_options.PdfRasterDpi.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-l");
            startInfo.ArgumentList.Add(
                _options.PdfRasterMaxPages.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add(outputPrefix);

            using var process = new Process { StartInfo = startInfo };

            if (!process.Start())
            {
                return new PdfRasterization(
                    [],
                    Available: true,
                    Failed: true,
                    "El rasterizador PDF no pudo iniciarse.");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.PdfRasterTimeoutSeconds));
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);

            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }

            _ = await stdoutTask.ConfigureAwait(false);
            _ = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                return new PdfRasterization(
                    [],
                    Available: true,
                    Failed: true,
                    "El rasterizador PDF terminó con error.");
            }

            var pages = Directory
                .EnumerateFiles(directory, "pagina*.png", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllBytes)
                .Where(page => page.Length > 0)
                .ToArray();

            return pages.Length == 0
                ? new PdfRasterization(
                    [],
                    Available: true,
                    Failed: true,
                    "El PDF no produjo páginas rasterizadas.")
                : new PdfRasterization(
                    pages,
                    Available: true,
                    Failed: false,
                    Detail: string.Empty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "La rasterización PDF superó el tiempo límite. TraceId={TraceId}",
                Activity.Current?.Id);
            return new PdfRasterization(
                [],
                Available: true,
                Failed: true,
                "El rasterizador PDF superó el tiempo límite.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "La rasterización PDF falló. Tipo={ExceptionType} TraceId={TraceId}",
                exception.GetType().FullName,
                Activity.Current?.Id);
            return new PdfRasterization(
                [],
                Available: true,
                Failed: true,
                "El rasterizador PDF no pudo procesar el documento.");
        }
        finally
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception)
            {
                // La limpieza temporal no altera el resultado.
            }
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // El proceso ya terminó o no puede terminarse.
        }
    }
}
