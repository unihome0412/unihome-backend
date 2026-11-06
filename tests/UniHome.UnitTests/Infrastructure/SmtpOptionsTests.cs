using UniHome.Infrastructure.Configuration;

namespace UniHome.UnitTests.Infrastructure;

public sealed class SmtpOptionsTests
{
    private const string Password = "clave-smtp-de-prueba-no-real";

    private static readonly SmtpOptionsValidator Validator = new();

    [Fact]
    public void Validate_ConConfiguracionCompleta_EsCorrecta()
    {
        var result = Validator.Validate(name: null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_SinHost_Falla()
    {
        var options = ValidOptions();
        options.Host = null;

        var failures = Failures(options);

        Assert.Contains(failures, failure => failure.Contains("Smtp:Host", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_SinUsername_Falla()
    {
        var options = ValidOptions();
        options.Username = "   ";

        var failures = Failures(options);

        Assert.Contains(failures, failure => failure.Contains("Smtp:Username", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_SinPassword_Falla()
    {
        var options = ValidOptions();
        options.Password = null;

        var failures = Failures(options);

        Assert.Contains(failures, failure => failure.Contains("Smtp:Password", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_SinFromEmail_Falla()
    {
        var options = ValidOptions();
        options.FromEmail = null;

        var failures = Failures(options);

        Assert.Contains(failures, failure => failure.Contains("Smtp:FromEmail", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ConFromEmailInvalido_Falla()
    {
        var options = ValidOptions();
        options.FromEmail = "no-es-un-correo";

        var failures = Failures(options);

        Assert.Contains(
            failures,
            failure => failure.Contains("Smtp:FromEmail", StringComparison.Ordinal) &&
                       failure.Contains("formato", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_SinStartTls_Falla()
    {
        var options = ValidOptions();
        options.UseStartTls = false;

        var failures = Failures(options);

        Assert.Contains(
            failures,
            failure => failure.Contains("Smtp:UseStartTls", StringComparison.Ordinal) &&
                       failure.Contains("sin cifrar", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(465)]
    [InlineData(2525)]
    public void Validate_ConPuertoDistintoDe587_Falla(int port)
    {
        var options = ValidOptions();
        options.Port = port;

        var failures = Failures(options);

        Assert.Contains(
            failures,
            failure => failure.Contains("Smtp:Port", StringComparison.Ordinal) &&
                       failure.Contains("587", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(121)]
    public void Validate_ConTimeoutFueraDeRango_Falla(int timeoutSeconds)
    {
        var options = ValidOptions();
        options.TimeoutSeconds = timeoutSeconds;

        var failures = Failures(options);

        Assert.Contains(
            failures,
            failure => failure.Contains("Smtp:TimeoutSeconds", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_NoExponeElPasswordEnLosMensajes()
    {
        var options = ValidOptions();
        options.Host = null;
        options.Username = null;
        options.FromEmail = null;

        var failures = Failures(options);
        var text = string.Join(" ", failures);

        Assert.DoesNotContain(Password, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Smtp:Password=", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TimeoutPredeterminado_EsSeguro()
    {
        var options = new SmtpOptions();

        Assert.Equal(SmtpOptions.RequiredPort, options.Port);
        Assert.True(options.UseStartTls);
        Assert.Equal(TimeSpan.FromSeconds(SmtpOptions.DefaultTimeoutSeconds), options.Timeout);
        Assert.Equal("UniHome", options.FromDisplayName);
    }

    private static string[] Failures(SmtpOptions options)
    {
        var result = Validator.Validate(name: null, options);

        Assert.True(result.Failed);
        return result.Failures?.ToArray() ?? [];
    }

    private static SmtpOptions ValidOptions() =>
        new()
        {
            Host = "smtp-relay.brevo.com",
            Port = SmtpOptions.RequiredPort,
            UseStartTls = true,
            Username = "usuario-smtp@example.test",
            Password = Password,
            FromEmail = "no-reply@unihome.test",
            FromName = "UniHome",
            TimeoutSeconds = SmtpOptions.DefaultTimeoutSeconds
        };
}
