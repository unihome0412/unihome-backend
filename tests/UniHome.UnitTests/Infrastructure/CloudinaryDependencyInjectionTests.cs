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
