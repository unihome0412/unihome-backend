using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniHome.Application.Abstractions.Storage;
using UniHome.Application.Identity.Abstractions;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Email;
using UniHome.Infrastructure.Identity;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Accounts;
using UniHome.Infrastructure.Persistence.Mongo.Sessions;

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

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName));

        // La sección Smtp es opcional para desarrollo y pruebas. Si existe
        // cualquier clave, se valida completa al arrancar y no se permite usar
        // texto plano ni credenciales parciales.
        var smtpSection = configuration.GetSection(SmtpOptions.SectionName);
        var smtpConfigured = smtpSection.GetChildren().Any();

        var smtpOptionsBuilder = services
            .AddOptions<SmtpOptions>()
            .Bind(smtpSection);

        if (smtpConfigured)
        {
            services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
            smtpOptionsBuilder.ValidateOnStart();

            services.AddSingleton<ISmtpClientFactory, MailKitSmtpClientFactory>();
            services.AddSingleton<SmtpEmailTransport>();
            services.AddSingleton<SmtpVerificationEmailSender>();
        }

        services.AddSingleton<MongoDbClientProvider>();
        services.AddSingleton<IStorageReadinessProbe, MongoStorageReadinessProbe>();

        // M01: puertos de identidad y sus adaptadores.
        services.AddSingleton<IAccountRepository, MongoAccountRepository>();
        services.AddSingleton<IRefreshSessionRepository, MongoRefreshSessionRepository>();
        services.AddSingleton<IPasswordHasher, AspNetCorePasswordHasher>();
        services.AddSingleton<IDummyPasswordHashProvider, DummyPasswordHashProvider>();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IVerificationCodeGenerator, RandomVerificationCodeGenerator>();
        services.AddSingleton<IVerificationCodeProtector, HmacVerificationCodeProtector>();

        if (smtpConfigured)
        {
            services.AddSingleton<IVerificationEmailSender>(serviceProvider =>
                serviceProvider.GetRequiredService<SmtpVerificationEmailSender>());
        }
        else
        {
            services.AddSingleton<IVerificationEmailSender, UnconfiguredVerificationEmailSender>();
        }

        services.AddSingleton<IRefreshTokenGenerator, SecureRefreshTokenGenerator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IAuthTokenSettings, JwtTokenSettings>();

        return services;
    }
}
