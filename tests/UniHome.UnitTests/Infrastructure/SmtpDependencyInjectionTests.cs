using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniHome.Application.Identity.Abstractions;
using UniHome.Infrastructure;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Email;
using UniHome.Infrastructure.Identity;

namespace UniHome.UnitTests.Infrastructure;

public sealed class SmtpDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_SinSmtp_RegistraElAdaptadorSinProveedor()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        var sender = provider.GetRequiredService<IVerificationEmailSender>();

        Assert.IsType<UnconfiguredVerificationEmailSender>(sender);
    }

    [Fact]
    public void AddInfrastructure_ConSmtpValido_RegistraElAdaptadorSmtp()
    {
        using var provider = BuildProvider(ValidConfiguration());

        var sender = provider.GetRequiredService<IVerificationEmailSender>();
        var options = provider.GetRequiredService<IOptions<SmtpOptions>>().Value;

        Assert.IsType<SmtpVerificationEmailSender>(sender);
        Assert.Equal("smtp-relay.brevo.com", options.Host);
        Assert.Equal(SmtpOptions.RequiredPort, options.Port);
        Assert.True(options.UseStartTls);
        Assert.Equal("no-reply@unihome.test", options.FromEmail);
    }

    [Fact]
    public void AddInfrastructure_ConSmtpParcial_NoPermiteResolverElEnvio()
    {
        var configuration = new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp-relay.brevo.com"
        };

        using var provider = BuildProvider(configuration);

        Assert.IsType<SmtpVerificationEmailSender>(
            provider.GetRequiredService<IVerificationEmailSender>());

        var exception = Assert.Throws<OptionsValidationException>(
            () => _ = provider.GetRequiredService<IOptions<SmtpOptions>>().Value);

        Assert.Contains("Smtp:Username", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Smtp:Password", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clave-smtp", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddInfrastructure_ConStartTlsDeshabilitado_NoPermiteResolverElEnvio()
    {
        var configuration = ValidConfiguration();
        configuration["Smtp:UseStartTls"] = "false";

        using var provider = BuildProvider(configuration);

        Assert.IsType<SmtpVerificationEmailSender>(
            provider.GetRequiredService<IVerificationEmailSender>());

        var exception = Assert.Throws<OptionsValidationException>(
            () => _ = provider.GetRequiredService<IOptions<SmtpOptions>>().Value);

        Assert.Contains("Smtp:UseStartTls", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> ValidConfiguration() =>
        new()
        {
            ["Smtp:Host"] = "smtp-relay.brevo.com",
            ["Smtp:Port"] = "587",
            ["Smtp:UseStartTls"] = "true",
            ["Smtp:Username"] = "usuario-smtp@example.test",
            ["Smtp:Password"] = "clave-smtp-de-prueba-no-real",
            ["Smtp:FromEmail"] = "no-reply@unihome.test",
            ["Smtp:FromName"] = "UniHome",
            ["Smtp:TimeoutSeconds"] = "15"
        };
}
