using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniHome.Application.Abstractions.Storage;
using UniHome.Application.Identity.Abstractions;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Identity;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Accounts;

namespace UniHome.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra los adaptadores de Infrastructure. MongoDB no se valida al arrancar:
    /// la configuración ausente se refleja en /health/ready y no impide iniciar la API.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName));

        services
            .AddOptions<VerificationCodeOptions>()
            .Bind(configuration.GetSection(VerificationCodeOptions.SectionName));

        services.AddSingleton<MongoDbClientProvider>();
        services.AddSingleton<IStorageReadinessProbe, MongoStorageReadinessProbe>();

        // M01: puertos de identidad y sus adaptadores.
        services.AddSingleton<IAccountRepository, MongoAccountRepository>();
        services.AddSingleton<IPasswordHasher, AspNetCorePasswordHasher>();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IVerificationCodeGenerator, RandomVerificationCodeGenerator>();
        services.AddSingleton<IVerificationCodeProtector, HmacVerificationCodeProtector>();
        services.AddSingleton<IVerificationEmailSender, UnconfiguredVerificationEmailSender>();

        return services;
    }
}
