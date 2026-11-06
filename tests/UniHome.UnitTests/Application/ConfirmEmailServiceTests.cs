using UniHome.Application.Identity.ConfirmEmail;
using UniHome.Domain.Accounts;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ConfirmEmailServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Confirm_ConCodigoCorrecto_ConfirmaYPersiste()
    {
        var repository = new InMemoryAccountRepository();
        var account = CreateUnconfirmedAccount("ana@uni.edu", "123456");
        await repository.AddAsync(account);

        var service = CreateService(repository);
        var result = await service.ConfirmAsync(new ConfirmEmailCommand("ana@uni.edu", "123456"));

        Assert.Equal(ConfirmEmailStatus.Confirmed, result.Status);

        var stored = await repository.FindByEmailAsync(EmailAddress.Create("ana@uni.edu"));
        Assert.True(stored!.IsEmailConfirmed);
    }

    [Fact]
    public async Task Confirm_ConCorreoInexistente_DevuelveMismoResultadoQueCodigoIncorrecto()
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository);

        var result = await service.ConfirmAsync(
            new ConfirmEmailCommand("no-existe@uni.edu", "123456"));

        Assert.Equal(ConfirmEmailStatus.IncorrectOrUnknown, result.Status);
    }

    [Fact]
    public async Task Confirm_ConCodigoIncorrecto_DevuelveIncorrectOrUnknownYPersisteElIntento()
    {
        var repository = new InMemoryAccountRepository();
        var account = CreateUnconfirmedAccount("ana@uni.edu", "123456");
        await repository.AddAsync(account);

        var service = CreateService(repository);
        var result = await service.ConfirmAsync(new ConfirmEmailCommand("ana@uni.edu", "000000"));

        Assert.Equal(ConfirmEmailStatus.IncorrectOrUnknown, result.Status);

        var stored = await repository.FindByEmailAsync(EmailAddress.Create("ana@uni.edu"));
        Assert.Equal(1, stored!.EmailVerification!.FailedAttempts);
    }

    [Fact]
    public async Task Confirm_ConCodigoVencido_Devuelve410()
    {
        var repository = new InMemoryAccountRepository();
        var account = CreateUnconfirmedAccount("ana@uni.edu", "123456");
        await repository.AddAsync(account);

        var service = CreateService(repository, now: Now.Add(VerificationCode.Lifetime).AddMinutes(1));
        var result = await service.ConfirmAsync(new ConfirmEmailCommand("ana@uni.edu", "123456"));

        Assert.Equal(ConfirmEmailStatus.ExpiredCode, result.Status);
    }

    [Fact]
    public async Task Confirm_SobreCuentaYaConfirmada_EsIdempotente()
    {
        var repository = new InMemoryAccountRepository();
        var account = CreateUnconfirmedAccount("ana@uni.edu", "123456");
        account.ConfirmEmail(Protect("123456"), Now);
        await repository.AddAsync(account);

        var service = CreateService(repository, now: Now.AddMinutes(1));
        var result = await service.ConfirmAsync(new ConfirmEmailCommand("ana@uni.edu", "000000"));

        Assert.Equal(ConfirmEmailStatus.AlreadyConfirmed, result.Status);
    }

    [Theory]
    [InlineData(null, "123456")]
    [InlineData("ana@uni.edu", null)]
    [InlineData("no-es-un-correo", "123456")]
    [InlineData("ana@uni.edu", "12345")]
    [InlineData("ana@uni.edu", "abcdef")]
    public async Task Confirm_ConDatosInvalidos_DevuelveValidationFailed(string? email, string? code)
    {
        var repository = new InMemoryAccountRepository();
        var service = CreateService(repository);

        var result = await service.ConfirmAsync(new ConfirmEmailCommand(email, code));

        Assert.Equal(ConfirmEmailStatus.ValidationFailed, result.Status);
        Assert.NotEmpty(result.ValidationErrors);
    }

    private static readonly Base64VerificationCodeProtector CodeProtector = new();

    private static ConfirmEmailService CreateService(
        InMemoryAccountRepository repository,
        DateTimeOffset? now = null) =>
        new(repository, CodeProtector, new FixedClock(now ?? Now));

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
