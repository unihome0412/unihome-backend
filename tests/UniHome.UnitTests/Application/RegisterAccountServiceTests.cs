using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.RegisterAccount;
using UniHome.Domain.Accounts;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class RegisterAccountServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Register_ConDatosValidos_PersisteHashYEnviaCodigo()
    {
        var repository = new InMemoryAccountRepository();
        var hasher = new RecordingPasswordHasher();
        var sender = new RecordingVerificationEmailSender();
        var service = CreateService(repository, hasher, sender);

        var result = await service.RegisterAsync(ValidCommand());

        Assert.True(result.Succeeded);
        Assert.False(result.EmailAlreadyRegistered);
        Assert.True(result.VerificationEmailSent);
        Assert.Equal("ana@uni.edu", result.Email);

        var account = await repository.FindByEmailAsync(EmailAddress.Create("ana@uni.edu"));
        Assert.NotNull(account);
        Assert.Equal(result.AccountId, account.Id);
        Assert.Equal("Ana López", account.FullName);
        Assert.Equal("ana@uni.edu", account.Email.Value);
        Assert.Equal(new DateOnly(2004, 5, 10), account.BirthDate);
        Assert.Equal(Gender.Mujer, account.Gender);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.False(account.IsEmailConfirmed);
        Assert.Equal("2026-01", account.PrivacyAcceptance.Version);
        Assert.Equal(Now, account.PrivacyAcceptance.AcceptedAt);

        Assert.Equal("hash:ClaveSegura1", account.PasswordHash);
        Assert.NotEqual("ClaveSegura1", account.PasswordHash);

        Assert.NotNull(account.EmailVerification);
        Assert.Equal("protected:012345", account.EmailVerification.CodeHash);
        Assert.NotEqual("012345", account.EmailVerification.CodeHash);

        var message = Assert.Single(sender.Messages);
        Assert.Equal("ana@uni.edu", message.Email);
        Assert.Equal("012345", message.Code);
        Assert.Equal(1, repository.Count);
    }

    [Fact]
    public async Task Register_ConCorreoDuplicado_DevuelveConflictoSinSegundaCuenta()
    {
        var repository = new InMemoryAccountRepository();
        var hasher = new RecordingPasswordHasher();
        var sender = new RecordingVerificationEmailSender();
        var service = CreateService(repository, hasher, sender);

        await service.RegisterAsync(ValidCommand());
        var result = await service.RegisterAsync(ValidCommand(email: "  ANA@UNI.EDU  "));

        Assert.True(result.EmailAlreadyRegistered);
        Assert.False(result.Succeeded);
        Assert.Equal(1, repository.Count);
        Assert.Single(sender.Messages);

        // El hash se calcula antes de escribir para no dar una señal de tiempo
        // distinta entre un correo nuevo y uno duplicado.
        Assert.Equal(2, hasher.HashCalls);
    }

    [Fact]
    public async Task Register_ConContrasenaCorta_DevuelveErrorPorCampo()
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository);

        var result = await service.RegisterAsync(ValidCommand(password: "corta"));

        Assert.False(result.Succeeded);
        Assert.Contains("contrasena", result.ValidationErrors.Keys);
        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public async Task Register_ConConfirmacionDistinta_DevuelveErrorPorCampo()
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository);

        var result = await service.RegisterAsync(ValidCommand(confirmPassword: "OtraClave1"));

        Assert.False(result.Succeeded);
        Assert.Contains("confirmarContrasena", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Register_SinAceptarPrivacidad_DevuelveErrorPorCampo()
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository);

        var result = await service.RegisterAsync(ValidCommand(acceptedPrivacy: false));

        Assert.False(result.Succeeded);
        Assert.Contains("aceptoAvisoPrivacidad", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Register_ConFechaFutura_DevuelveErrorPorCampo()
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository);

        var result = await service.RegisterAsync(
            ValidCommand(birthDate: DateOnly.FromDateTime(Now.UtcDateTime).AddDays(1)));

        Assert.False(result.Succeeded);
        Assert.Contains("fechaNacimiento", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Register_ConCorreoInvalido_DevuelveErrorPorCampo()
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository);

        var result = await service.RegisterAsync(ValidCommand(email: "no-es-un-correo"));

        Assert.False(result.Succeeded);
        Assert.Contains("correo", result.ValidationErrors.Keys);
    }

    [Fact]
    public void Command_ToString_NoExponeCredencialesNiDatosPersonales()
    {
        var text = ValidCommand().ToString();

        Assert.DoesNotContain("ClaveSegura1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ana@uni.edu", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ana López", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_ConContrasenaDemasiadoLarga_DevuelveErrorPorCampo()
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository);

        var result = await service.RegisterAsync(
            ValidCommand(password: new string('p', 129), confirmPassword: new string('p', 129)));

        Assert.False(result.Succeeded);
        Assert.Contains("contrasena", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Register_ConVersionDePrivacidadDemasiadoLarga_DevuelveErrorPorCampo()
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository);

        var result = await service.RegisterAsync(
            ValidCommand(privacyVersion: new string('v', PrivacyAcceptance.MaxVersionLength + 1)));

        Assert.False(result.Succeeded);
        Assert.Contains("versionAvisoPrivacidad", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Register_ConCaracteresDeControlEnElNombre_DevuelveErrorPorCampo()
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository);

        var result = await service.RegisterAsync(ValidCommand(fullName: "Ana\u0000López"));

        Assert.False(result.Succeeded);
        Assert.Contains("nombreCompleto", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Register_ConNombreCorto_DevuelveErrorPorCampo()
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository);

        var result = await service.RegisterAsync(ValidCommand(fullName: "A"));

        Assert.False(result.Succeeded);
        Assert.Contains("nombreCompleto", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Register_SinVersionDePrivacidad_DevuelveErrorPorCampo()
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository);

        var result = await service.RegisterAsync(ValidCommand(privacyVersion: "   "));

        Assert.False(result.Succeeded);
        Assert.Contains("versionAvisoPrivacidad", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Register_SiElEnvioFalla_ConservaLaCuentaEInformaNoEnviado()
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository, sender: new ThrowingVerificationEmailSender());

        var result = await service.RegisterAsync(ValidCommand());

        Assert.True(result.Succeeded);
        Assert.False(result.VerificationEmailSent);
        Assert.Equal(1, repository.Count);
    }

    private static RegisterAccountService CreateService(
        InMemoryAccountRepository repository,
        RecordingPasswordHasher? hasher = null,
        IVerificationEmailSender? sender = null) =>
        new(
            repository,
            hasher ?? new RecordingPasswordHasher(),
            new FixedClock(Now),
            new FixedVerificationCodeGenerator("012345"),
            new PrefixVerificationCodeProtector(),
            sender ?? new RecordingVerificationEmailSender());

    private static RegisterAccountCommand ValidCommand(
        string fullName = "  Ana López  ",
        string email = "  Ana@Uni.EDU  ",
        DateOnly? birthDate = null,
        Gender? gender = null,
        string password = "ClaveSegura1",
        string confirmPassword = "ClaveSegura1",
        bool acceptedPrivacy = true,
        string privacyVersion = " 2026-01 ") =>
        new(
            fullName,
            email,
            birthDate ?? new DateOnly(2004, 5, 10),
            gender ?? Gender.Mujer,
            password,
            confirmPassword,
            acceptedPrivacy,
            privacyVersion);
}
