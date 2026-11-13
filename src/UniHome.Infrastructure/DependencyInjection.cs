using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniHome.Application.Abstractions.RateLimiting;
using UniHome.Application.Abstractions.Storage;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Email;
using UniHome.Infrastructure.Identity;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Accounts;
using UniHome.Infrastructure.Persistence.Mongo.PasswordResets;
using UniHome.Infrastructure.Persistence.Mongo.Sessions;
using UniHome.Infrastructure.Persistence.Mongo.Universities;
using UniHome.Infrastructure.Redis;

namespace UniHome.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra los adaptadores de Infrastructure. MongoDB no se valida al arrancar:
    /// la configuración ausente se refleja en /health/ready y no impide iniciar la API.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = true)
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
            .AddOptions<PasswordRecoveryOptions>()
            .Bind(configuration.GetSection(PasswordRecoveryOptions.SectionName));

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName));

        services
            .AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName));

        var redisSection = configuration.GetSection(RedisOptions.SectionName);
        var redisOptions = redisSection.Get<RedisOptions>() ?? new RedisOptions();

        // En entornos fuera de Development (Producción/Staging con réplicas múltiples),
        // Redis es estrictamente obligatorio para evitar contadores locales independientes en memoria.
        if (!isDevelopment)
        {
            if (!redisOptions.Enabled)
            {
                throw new InvalidOperationException(
                    "Redis distribuido es obligatorio fuera del entorno de Development para evitar contadores locales " +
                    "independientes entre réplicas. Configure Redis:Enabled=true y proporcione las credenciales de Upstash.");
            }

            var hasCredentials = !string.IsNullOrWhiteSpace(redisOptions.ConnectionString) ||
                                 (!string.IsNullOrWhiteSpace(redisOptions.RestUrl) && !string.IsNullOrWhiteSpace(redisOptions.RestToken)) ||
                                 (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UPSTASH_REDIS_REST_URL")) && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UPSTASH_REDIS_REST_TOKEN"))) ||
                                 !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REDIS_CONNECTIONSTRING")) ||
                                 !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Redis__ConnectionString"));

            if (!hasCredentials)
            {
                throw new InvalidOperationException(
                    "Redis:Enabled está activo pero no se configuraron credenciales válidas fuera de Development. " +
                    "Configure Redis:ConnectionString o Redis:RestUrl y Redis:RestToken.");
            }
        }

        if (redisOptions.Enabled)
        {
            services.AddSingleton<IDistributedRateLimiter, UpstashRedisDistributedRateLimiter>();
        }
        else
        {
            services.AddSingleton<IDistributedRateLimiter, InMemoryDistributedRateLimiter>();
        }

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
        services.AddSingleton<IPasswordResetRepository, MongoPasswordResetRepository>();
        services.AddSingleton<IUniversityRepository, MongoUniversityRepository>();
        services.AddSingleton<IPasswordHasher, AspNetCorePasswordHasher>();
        services.AddSingleton<IDummyPasswordHashProvider, DummyPasswordHashProvider>();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IVerificationCodeGenerator, RandomVerificationCodeGenerator>();
        services.AddSingleton<IVerificationCodeProtector, HmacVerificationCodeProtector>();
        services.AddSingleton<IPasswordResetTokenGenerator, SecurePasswordResetTokenGenerator>();

        if (smtpConfigured)
        {
            services.AddSingleton<IVerificationEmailSender>(serviceProvider =>
                serviceProvider.GetRequiredService<SmtpVerificationEmailSender>());
            services.AddSingleton<IPasswordResetEmailSender, SmtpPasswordResetEmailSender>();
        }
        else
        {
            services.AddSingleton<IVerificationEmailSender, UnconfiguredVerificationEmailSender>();
            services.AddSingleton<IPasswordResetEmailSender, UnconfiguredPasswordResetEmailSender>();
        }

        services.AddSingleton<IRefreshTokenGenerator, SecureRefreshTokenGenerator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IAuthTokenSettings, JwtTokenSettings>();

        return services;
    }
}
