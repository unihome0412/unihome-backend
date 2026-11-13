using Microsoft.Extensions.Options;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Storage;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Prueba real de Cloudinary Free. Si faltan credenciales seguras se omite y se
/// reporta como PENDIENTE, nunca como aprobada.
/// </summary>
public sealed class CloudinaryPersistenceTests
{
    private static readonly byte[] Png =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    [CloudinaryIntegrationFact]
    public async Task SubidaLecturaYEliminacionReales_DebenFuncionar()
    {
        var options = Options.Create(new CloudinaryOptions
        {
            CloudName = CloudinaryIntegrationEnvironment.CloudName,
            ApiKey = CloudinaryIntegrationEnvironment.ApiKey,
            ApiSecret = CloudinaryIntegrationEnvironment.ApiSecret,
            Folder = "unihome/integration-tests",
            TimeoutSeconds = 30
        });
        using var client = new CloudinaryPhotoClient(options);
        var storage = new CloudinaryUnitPhotoStorage(client, options);
        string? storageKey = null;

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
            Assert.NotEmpty(memory.ToArray());

            Assert.Contains("/api/v1/fotos/", storage.GetReadUrl(stored.StorageKey));
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
