using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Analysis;

internal sealed record OcrExecution(
    string? Text,
    bool Available,
    bool Failed,
    string Detail);

/// <summary>
/// Adaptador al ejecutable Tesseract. No empaqueta un motor nativo ni simula
/// resultados: si el binario o el idioma no están instalados, devuelve
/// Available=false. Los argumentos se pasan sin shell y el archivo temporal se
/// elimina siempre.
/// </summary>
internal sealed class TesseractCliOcrEngine
{
    private readonly DocumentAnalysisOptions _options;
    private readonly ILogger<TesseractCliOcrEngine> _logger;

    public TesseractCliOcrEngine(
        IOptions<DocumentAnalysisOptions> options,
        ILogger<TesseractCliOcrEngine> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OcrExecution> ReadAsync(
        byte[] content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var executable = ResolveExecutable(_options.TesseractExecutablePath);

        if (executable is null)
        {
            return new OcrExecution(
                Text: null,
                Available: false,
                Failed: false,
                Detail: "El motor OCR local (Tesseract) no está instalado.");
        }

        var extension = contentType == "image/png" ? ".png" : ".jpg";
        var directory = Path.Combine(
            Path.GetTempPath(),
            "unihome-ocr",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var inputPath = Path.Combine(directory, $"documento{extension}");

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
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("stdout");
            startInfo.ArgumentList.Add("-l");
            startInfo.ArgumentList.Add(_options.TesseractLanguage);
            startInfo.ArgumentList.Add("--psm");
            startInfo.ArgumentList.Add(
                _options.TesseractPageSegmentationMode.ToString(CultureInfo.InvariantCulture));

            using var process = new Process { StartInfo = startInfo };

            if (!process.Start())
            {
                return new OcrExecution(
                    null,
                    Available: true,
                    Failed: true,
                    "El motor OCR no pudo iniciarse.");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.TesseractTimeoutSeconds));

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

            var stdout = await stdoutTask.ConfigureAwait(false);
            _ = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                return new OcrExecution(
                    null,
                    Available: true,
                    Failed: true,
                    "El motor OCR terminó con error.");
            }

            return new OcrExecution(
                stdout.Trim(),
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
                "El OCR local superó el tiempo límite. TraceId={TraceId}",
                Activity.Current?.Id);
            return new OcrExecution(
                null,
                Available: true,
                Failed: true,
                "El motor OCR superó el tiempo límite.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "El OCR local falló. Tipo={ExceptionType} TraceId={TraceId}",
                exception.GetType().FullName,
                Activity.Current?.Id);
            return new OcrExecution(
                null,
                Available: true,
                Failed: true,
                "El motor OCR no pudo procesar el documento.");
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
                // La limpieza temporal no debe alterar el resultado del análisis.
            }
        }
    }

    internal static string? ResolveExecutable(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        var candidate = configuredPath.Trim();

        if (Path.IsPathRooted(candidate))
        {
            return File.Exists(candidate) ? candidate : null;
        }

        var path = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var file = Path.Combine(directory, candidate);

            if (File.Exists(file))
            {
                return file;
            }

            if (OperatingSystem.IsWindows() && File.Exists(file + ".exe"))
            {
                return file + ".exe";
            }
        }

        return null;
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
            // El proceso ya terminó o no puede terminarse; no se registra la ruta.
        }
    }
}
