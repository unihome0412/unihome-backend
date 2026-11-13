using Microsoft.Extensions.Options;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Units.Photos;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Storage;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Infrastructure;

public sealed class CloudinaryUnitPhotoStorageTests
{
    private static readonly byte[] Png =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    [Fact]
    public async Task Save_DebeSubirComoAuthenticatedYGuardarHash()
    {
        var client = new FakeCloudinaryPhotoClient();
        var storage = CreateStorage(client);
        await using var stream = new MemoryStream(Png);

        var stored = await storage.SaveAsync(stream, "image/png");

        Assert.Single(client.Uploaded);
        Assert.StartsWith("unihome/unidades/", stored.StorageKey);
        Assert.Equal("image/png", stored.ContentType);
        Assert.Equal(Png.Length, stored.SizeBytes);
        Assert.False(string.IsNullOrWhiteSpace(stored.Sha256));
    }

    [Fact]
    public async Task Save_ArchivoMayorA5Mb_DebeFallarCerrado()
    {
        var storage = CreateStorage(new FakeCloudinaryPhotoClient());
        var tooLarge = new byte[CloudinaryUnitPhotoStorage.MaxPhotoBytes + 1];

        await Assert.ThrowsAsync<StorageUnavailableException>(() =>
            storage.SaveAsync(new MemoryStream(tooLarge), "image/jpeg"));
    }

    [Fact]
    public async Task Save_ErrorDelProveedor_DebeTraducirseA503()
    {
        var client = new FakeCloudinaryPhotoClient { UploadFails = true };
        var storage = CreateStorage(client);

        await Assert.ThrowsAsync<StorageUnavailableException>(() =>
            storage.SaveAsync(new MemoryStream(Png), "image/png"));
    }

    [Fact]
    public async Task Save_Cancelacion_DebePropagarse()
    {
        var client = new FakeCloudinaryPhotoClient { UploadCancelled = true };
        var storage = CreateStorage(client);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            storage.SaveAsync(new MemoryStream(Png), "image/png", cts.Token));
    }

    [Fact]
    public async Task Read_Existente_DebeDevolverContenido()
    {
        var client = new FakeCloudinaryPhotoClient
        {
            DownloadContent = Png,
            DownloadContentType = "image/png"
        };
        var storage = CreateStorage(client);

        var content = await storage.ReadAsync("unihome/unidades/foto");

        Assert.NotNull(content);
        Assert.Equal("image/png", content.ContentType);
        Assert.Equal(Png.Length, content.ContentLength);
        await using var stream = content.Content;
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        Assert.Equal(Png, memory.ToArray());
    }

    [Fact]
    public async Task Read_Inexistente_DebeDevolverNull()
    {
        var storage = CreateStorage(new FakeCloudinaryPhotoClient());

        Assert.Null(await storage.ReadAsync("unihome/unidades/inexistente"));
    }

    [Fact]
    public async Task Delete_ErrorDelProveedor_DebeTraducirseA503()
    {
        var client = new FakeCloudinaryPhotoClient { DeleteFails = true };
        var storage = CreateStorage(client);

        await Assert.ThrowsAsync<StorageUnavailableException>(() =>
            storage.DeleteAsync("unihome/unidades/foto"));
    }

    [Fact]
    public void GetReadUrl_DebeUsarProxyYNoExponerCloudinary()
    {
        var storage = CreateStorage(
            new FakeCloudinaryPhotoClient(),
            publicBaseUrl: "https://api.unihome.test");
        var storageKey = "unihome/unidades/foto";
        var reference = PhotoReference.Encode(storageKey);

        var url = storage.GetReadUrl(storageKey);

        Assert.Equal($"https://api.unihome.test/api/v1/fotos/{reference}", url);
        Assert.DoesNotContain("cloudinary", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetReadUrl_SinBase_DebeDevolverRutaRelativa()
    {
        var storage = CreateStorage(new FakeCloudinaryPhotoClient());

        var url = storage.GetReadUrl("unihome/unidades/foto");

        Assert.StartsWith("/api/v1/fotos/", url);
    }

    private static CloudinaryUnitPhotoStorage CreateStorage(
        FakeCloudinaryPhotoClient client,
        string? publicBaseUrl = null) =>
        new(
            client,
            Options.Create(new CloudinaryOptions
            {
                CloudName = "test-cloud",
                ApiKey = "test-key",
                ApiSecret = "test-secret",
                Folder = "unihome/unidades",
                PublicBaseUrl = publicBaseUrl,
                TimeoutSeconds = 5
            }));
}
