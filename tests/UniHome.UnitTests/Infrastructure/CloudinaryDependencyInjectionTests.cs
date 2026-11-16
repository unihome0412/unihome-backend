using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Units.Abstractions;
using UniHome.Infrastructure;
using UniHome.Infrastructure.Storage;

namespace UniHome.UnitTests.Infrastructure;

public sealed class CloudinaryDependencyInjectionTests
{
    [Fact]
    public void SinConfiguracion_DebeResolverUnconfiguredYSoloFallarSiSePideCloudinary()
    {
        var services = BuildServices(new Dictionary<string, string?>());

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IUnitPhotoStorage));
        Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(ICloudinaryPhotoClient));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<UnconfiguredUnitPhotoStorage>(
            provider.GetRequiredService<IUnitPhotoStorage>());
        Assert.Throws<StorageUnavailableException>(() =>
            provider.GetRequiredService<ICloudinaryPhotoClient>());
    }

    [Fact]
    public void ConAlmacenamientoEfimeroEnDesarrollo_NoResuelveCloudinary()
    {
        var services = BuildServices(new Dictionary<string, string?>
        {
            ["Cloudinary:UseEphemeralStorage"] = "true"
        });

        using var provider = services.BuildServiceProvider();
        Assert.IsType<EphemeralUnitPhotoStorage>(
            provider.GetRequiredService<IUnitPhotoStorage>());
        Assert.Throws<StorageUnavailableException>(() =>
            provider.GetRequiredService<ICloudinaryPhotoClient>());
    }

    [Fact]
    public async Task AlmacenamientoEfimero_GuardaLeeYEliminaSinDisco()
    {
        var services = BuildServices(new Dictionary<string, string?>
        {
            ["Cloudinary:UseEphemeralStorage"] = "true"
        });

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IUnitPhotoStorage>();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01 };

        using var input = new MemoryStream(png, writable: false);
        var stored = await storage.SaveAsync(input, "image/png");

        Assert.StartsWith("ephemeral/", stored.StorageKey, StringComparison.Ordinal);
        Assert.Equal("image/png", stored.ContentType);
        Assert.Equal(png.LongLength, stored.SizeBytes);
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(png)),
            stored.Sha256);

        var content = await storage.ReadAsync(stored.StorageKey);
        Assert.NotNull(content);
        Assert.Equal("image/png", content.ContentType);
        using var output = new MemoryStream();
        await content.Content.CopyToAsync(output);
        Assert.Equal(png, output.ToArray());
        Assert.StartsWith(
            "/api/v1/fotos/",
            storage.GetReadUrl(stored.StorageKey),
            StringComparison.Ordinal);

        await storage.DeleteAsync(stored.StorageKey);
        Assert.Null(await storage.ReadAsync(stored.StorageKey));
    }

    [Fact]
    public void ConConfiguracionCompleta_DebeResolverCloudinaryYUnSoloStorage()
    {
        var services = BuildServices(new Dictionary<string, string?>
        {
            ["Cloudinary:CloudName"] = "test-cloud",
            ["Cloudinary:ApiKey"] = "test-key",
            ["Cloudinary:ApiSecret"] = "test-secret",
            ["Cloudinary:Folder"] = "unihome/unidades",
            ["Cloudinary:TimeoutSeconds"] = "5"
        });

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IUnitPhotoStorage));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ICloudinaryPhotoClient));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<CloudinaryUnitPhotoStorage>(
            provider.GetRequiredService<IUnitPhotoStorage>());
        Assert.IsType<CloudinaryPhotoClient>(
            provider.GetRequiredService<ICloudinaryPhotoClient>());
    }

    private static ServiceCollection BuildServices(
        IReadOnlyDictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration, isDevelopment: true);
        return services;
    }
}
