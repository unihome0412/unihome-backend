using UniHome.Application.Identity.ConfirmEmail;
using UniHome.Domain.Accounts;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ResendConfirmationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Resend_ConCuentaSinConfirmar_EmiteNuevoCodigoYEnviaCorreo()
    {
        var repository = new InMemoryAccountRepository();
        var account = CreateUnconfirmedAccount("ana@uni.edu", "123456");
        await repository.AddAsync(account);
        var sender = new RecordingVerificationEmailSender();

        var service = CreateService(repository, sender: sender, codeGenerator: new FixedVerificationCodeGenerator("654321"));
        var result = await service.ResendAsync(new ResendConfirmationCommand("ana@uni.edu"));

        Assert.Equal(ResendConfirmationStatus.Accepted, result.Status);
        Assert.Single(sender.Messages, message => message.Email == "ana@uni.edu" && message.Code == "654321");

        var stored = await repository.FindByEmailAsync(EmailAddress.Create("ana@uni.edu"));
        Assert.Equal(Protect("654321"), stored!.EmailVerification!.CodeHash);
    }

    [Fact]
    public async Task Resend_ConCorreoInexistente_DevuelveAcceptedSinEnviarCorreo()
    {
        var repository = new InMemoryAccountRepository();
        var sender = new RecordingVerificationEmailSender();
        var service = CreateService(repository, sender: sender);

        var result = await service.ResendAsync(new ResendConfirmationCommand("no-existe@uni.edu"));

        Assert.Equal(ResendConfirmationStatus.Accepted, result.Status);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Resend_SobreCuentaYaConfirmada_DevuelveAcceptedSinEnviarCorreo()
    {
        var repository = new InMemoryAccountRepository();
        var account = CreateUnconfirmedAccount("ana@uni.edu", "123456");
        account.ConfirmEmail(Protect("123456"), Now);
        await repository.AddAsync(account);
        var sender = new RecordingVerificationEmailSender();

        var service = CreateService(repository, sender: sender);
        var result = await service.ResendAsync(new ResendConfirmationCommand("ana@uni.edu"));

        Assert.Equal(ResendConfirmationStatus.Accepted, result.Status);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Resend_TrasAgotarElLimitePorCuenta_DevuelveAcceptedSinEnviarCorreo()
    {
        var repository = new InMemoryAccountRepository();
        var account = CreateUnconfirmedAccount("ana@uni.edu", "123456");
        account.ResendEmailVerification(Protect("111111"), Now.AddSeconds(1));
        account.ResendEmailVerification(Protect("222222"), Now.AddSeconds(2));
        account.ResendEmailVerification(Protect("333333"), Now.AddSeconds(3));
        await repository.AddAsync(account);
        var sender = new RecordingVerificationEmailSender();

        var service = CreateService(repository, sender: sender, now: Now.AddSeconds(4));
        var result = await service.ResendAsync(new ResendConfirmationCommand("ana@uni.edu"));

        Assert.Equal(ResendConfirmationStatus.Accepted, result.Status);
        Assert.Empty(sender.Messages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-correo")]
    public async Task Resend_ConCorreoMalFormado_DevuelveValidationFailed(string? email)
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository);

        var result = await service.ResendAsync(new ResendConfirmationCommand(email));

        Assert.Equal(ResendConfirmationStatus.ValidationFailed, result.Status);
        Assert.Contains("correo", result.ValidationErrors.Keys);
    }

    private static readonly Base64VerificationCodeProtector CodeProtector = new();

    private static ResendConfirmationService CreateService(
        InMemoryAccountRepository repository,
        RecordingVerificationEmailSender? sender = null,
        FixedVerificationCodeGenerator? codeGenerator = null,
        DateTimeOffset? now = null) =>
        new(
            repository,
            codeGenerator ?? new FixedVerificationCodeGenerator("999999"),
            CodeProtector,
            sender ?? new RecordingVerificationEmailSender(),
            new FixedClock(now ?? Now));

    private static Account CreateUnconfirmedAccount(string email, string rawCode)
    {
        var account = Account.Register(
            Guid.NewGuid(),
            "Ana López",
            EmailAddress.Create(email),
            new DateOnly(2004, 5, 10),
            Gender.Mujer,
            "hash:ClaveSegura1",
            new PrivacyAcceptance("2026-01", Now),
            Now);

        account.IssueEmailVerification(Protect(rawCode), Now);
        return account;
    }

    private static string Protect(string code) => CodeProtector.Protect(code);
}
