using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UniHome.Application.Abstractions.RateLimiting;
using UniHome.Application.Abstractions.Storage;
using UniHome.Application.Favorites.Abstractions;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Publications.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Infrastructure.Analysis;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Email;
using UniHome.Infrastructure.Identity;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Accounts;
using UniHome.Infrastructure.Persistence.Mongo.Exploration;
using UniHome.Infrastructure.Persistence.Mongo.PasswordResets;
using UniHome.Infrastructure.Persistence.Mongo.Properties;
using UniHome.Infrastructure.Persistence.Mongo.Publications;
using UniHome.Infrastructure.Persistence.Mongo.Sessions;
using UniHome.Infrastructure.Persistence.Mongo.Units;
using UniHome.Infrastructure.Persistence.Mongo.Universities;
using UniHome.Infrastructure.Persistence.Mongo.Verification;
using UniHome.Infrastructure.Redis;
using UniHome.Infrastructure.Storage;
using UniHome.Infrastructure.Verification;

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

        services.Configure<CloudinaryOptions>(
            configuration.GetSection(CloudinaryOptions.SectionName));

        services
            .AddOptions<VerificationCodeOptions>()
            .Bind(configuration.GetSection(VerificationCodeOptions.SectionName));

        services
            .AddOptions<PasswordRecoveryOptions>()
            .Bind(configuration.GetSection(PasswordRecoveryOptions.SectionName));

        services
            .AddOptions<DocumentSecurityOptions>()
            .Bind(configuration.GetSection(DocumentSecurityOptions.SectionName))
            .Configure<IHostEnvironment>((options, environment) =>
            {
                if (string.IsNullOrWhiteSpace(options.EncryptionKey))
                {
                    if (!environment.IsDevelopment())
                    {
                        throw new InvalidOperationException(
                            $"Configure {DocumentSecurityOptions.SectionName}:EncryptionKey " +
                            "con 32 bytes en Base64 fuera de Development.");
                    }

                    options.EncryptionKey = DocumentSecurityOptions.CreateEphemeralKey();
                    options.EphemeralEncryptionKey = true;
                }
            })
            .Validate(
                options => options.HasValidEncryptionKey(),
                $"{DocumentSecurityOptions.SectionName}:EncryptionKey debe contener 32 bytes en Base64.")
            .Validate(
                options => options.IsValid(),
                $"{DocumentSecurityOptions.SectionName} contiene una vigencia, clave de URL o base pública inválida.")
            .ValidateOnStart();

        services
            .AddOptions<DocumentAnalysisOptions>()
            .Bind(configuration.GetSection(DocumentAnalysisOptions.SectionName))
            .Validate(
                options => options.IsValid(),
                $"{DocumentAnalysisOptions.SectionName} contiene límites de análisis inválidos.")
            .ValidateOnStart();

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName));

        services
            .AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .Validate(
                options => options.CommandTimeoutMs is >= RedisOptions.MinCommandTimeoutMs
                    and <= RedisOptions.MaxCommandTimeoutMs,
                $"{RedisOptions.SectionName}:CommandTimeoutMs debe estar entre " +
                $"{RedisOptions.MinCommandTimeoutMs} y {RedisOptions.MaxCommandTimeoutMs}.")
            .Validate(
                options => options.MaxAttempts is >= 1 and <= RedisOptions.MaxAttemptsLimit,
                $"{RedisOptions.SectionName}:MaxAttempts debe estar entre 1 y " +
                $"{RedisOptions.MaxAttemptsLimit}.")
            .Validate(
                options => options.RetryDelayMs is >= 0 and <= RedisOptions.MaxRetryDelayMs,
                $"{RedisOptions.SectionName}:RetryDelayMs debe estar entre 0 y " +
                $"{RedisOptions.MaxRetryDelayMs}.")
            .Validate(
                options => options.ReadinessTimeoutMs is >= RedisOptions.MinReadinessTimeoutMs
                    and <= RedisOptions.MaxReadinessTimeoutMs,
                $"{RedisOptions.SectionName}:ReadinessTimeoutMs debe estar entre " +
                $"{RedisOptions.MinReadinessTimeoutMs} y {RedisOptions.MaxReadinessTimeoutMs}.")
            .ValidateOnStart();

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

        // readiness consulta el mismo almacén que usa el rate limiting. En
        // Development usa el adaptador en memoria; en Production, Upstash real.
        services.AddSingleton<IRateLimiterReadinessProbe>(serviceProvider =>
            serviceProvider.GetRequiredService<IDistributedRateLimiter>());

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
        services.AddSingleton<IPropertyRepository, MongoPropertyRepository>();
        services.AddSingleton<IUnitRepository, MongoUnitRepository>();
        services.AddSingleton<ITermsAcceptanceRepository, MongoTermsAcceptanceRepository>();
        services.AddSingleton<IUnitFavoriteRepository, MongoFavoriteRepository>();
        services.AddSingleton<IVerificationRepository, MongoVerificationRepository>();
        services.AddSingleton<IVerificationAuditRepository, MongoVerificationAuditRepository>();
        services.AddSingleton<IVerificationDocumentStore, MongoVerificationDocumentStore>();
        services.AddSingleton<TesseractCliOcrEngine>();
        services.AddSingleton<IDocumentAnalyzer, LocalDocumentAnalyzer>();
        services.AddSingleton<IDocumentPreviewGenerator, SkiaDocumentPreviewGenerator>();
        services.AddSingleton<IDocumentUrlSigner, HmacDocumentUrlSigner>();
        services.AddHttpContextAccessor();

        // La decisi?n se eval?a al resolver, no al construir el contenedor: as? la
        // configuraci?n del host (incluidos tests y User Secrets) se aplica antes
        // de elegir el adaptador y nunca hay dos implementaciones compitiendo.
        services.AddSingleton<ICloudinaryPhotoClient>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<CloudinaryOptions>>()
                .Value;

            if (!options.IsConfigured)
            {
                throw new StorageUnavailableException();
            }

            return new CloudinaryPhotoClient(Options.Create(options));
        });

        services.AddSingleton<IUnitPhotoStorage>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<CloudinaryOptions>>()
                .Value;

            // Los harnesses DAST/RESTler se ejecutan en Development y necesitan
            // un backend real de fotos sin usar Cloudinary ni secretos. Fuera de
            // Development este flag se ignora y se conserva el fail-closed.
            if (isDevelopment && options.UseEphemeralStorage)
            {
                return new EphemeralUnitPhotoStorage();
            }

            if (!options.IsConfigured)
            {
                return new UnconfiguredUnitPhotoStorage();
            }

            return new CloudinaryUnitPhotoStorage(
                serviceProvider.GetRequiredService<ICloudinaryPhotoClient>(),
                Options.Create(options),
                serviceProvider.GetService<IHttpContextAccessor>());
        });
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
