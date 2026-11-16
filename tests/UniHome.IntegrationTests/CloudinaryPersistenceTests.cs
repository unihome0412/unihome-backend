using System.Net;
using Microsoft.Extensions.Options;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Storage;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Prueba real de Cloudinary Free. Si faltan credenciales se omite y se reporta
/// como PENDIENTE; si se activa con la bandera y faltan credenciales, falla.
/// </summary>
public sealed class CloudinaryPersistenceTests
{
    // PNG RGB 2x2 auténtico, sin texto, EXIF ni datos personales.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFElEQVR42mP4z8DAAMb//0MZUA4Aep8J9x7TI9wAAAAASUVORK5CYII=");

    [CloudinaryIntegrationFact]
    public async Task SubidaLecturaYEliminacionReales_DebenFuncionar()
    {
        var credentials = CloudinaryIntegrationEnvironment.RequireConfiguration();
        var options = Options.Create(new CloudinaryOptions
        {
            CloudName = credentials.CloudName,
            ApiKey = credentials.ApiKey,
            ApiSecret = credentials.ApiSecret,
            Folder = "unihome/integration-tests",
            TimeoutSeconds = 30
        });
        using var client = new CloudinaryPhotoClient(options);
        var storage = new CloudinaryUnitPhotoStorage(client, options);
        string? storageKey = null;
        string? authenticatedUrl = null;

        try
        {
            await using var upload = new MemoryStream(Png);
            var stored = await storage.SaveAsync(upload, "image/png");
            storageKey = stored.StorageKey;

            Assert.StartsWith("unihome/integration-tests/", stored.StorageKey);
            Assert.Equal(Png.Length, stored.SizeBytes);
            Assert.False(string.IsNullOrWhiteSpace(stored.Sha256));

            var content = await storage.ReadAsync(stored.StorageKey);
            Assert.NotNull(content);
            Assert.StartsWith("image/", content.ContentType, StringComparison.Ordinal);
            await using var downloaded = content.Content;
            using var memory = new MemoryStream();
            await downloaded.CopyToAsync(memory);
            Assert.Equal(Png, memory.ToArray());

            authenticatedUrl = storage.GetReadUrl(stored.StorageKey);
            Assert.Contains("/api/v1/fotos/", authenticatedUrl);

            // La URL directa sin firma no debe entregar el asset authenticated.
            var unsignedUrl =
                $"https://res.cloudinary.com/{Uri.EscapeDataString(credentials.CloudName)}" +
                $"/image/authenticated/{stored.StorageKey}";
            using var http = new HttpClient(new SocketsHttpHandler
            {
                AllowAutoRedirect = false
            });
            using var unsignedResponse = await http.GetAsync(unsignedUrl);
            Assert.Contains(
                unsignedResponse.StatusCode,
                new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(storageKey))
            {
                await storage.DeleteAsync(storageKey);
                Assert.Null(await storage.ReadAsync(storageKey));
            }
        }
    }
}
