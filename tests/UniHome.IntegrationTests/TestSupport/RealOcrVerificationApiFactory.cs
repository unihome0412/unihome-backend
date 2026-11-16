using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Infrastructure.Analysis;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Mantiene repositorios y almacenamiento en memoria, pero sustituye el analizador
/// falso por el analizador local real para validar OCR/ELA de extremo a extremo.
/// </summary>
public sealed class RealOcrVerificationApiFactory : UniHomeApiFactory
{
    public RealOcrVerificationApiFactory()
    {
        TesseractExecutable = RealOcrIntegrationFactAttribute.ResolveExecutable()
            ?? "tesseract";
    }

    public string TesseractExecutable { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentAnalysis:TesseractExecutablePath"] = TesseractExecutable,
                ["DocumentAnalysis:TesseractLanguage"] = "eng",
                ["DocumentAnalysis:TesseractTimeoutSeconds"] = "60",
                ["DocumentAnalysis:TesseractPageSegmentationMode"] = "6"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDocumentAnalyzer>();
            services.AddSingleton<IDocumentAnalyzer, LocalDocumentAnalyzer>();
        });
    }
}
