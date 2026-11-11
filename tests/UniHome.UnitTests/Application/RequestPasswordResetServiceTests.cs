using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.PasswordRecovery;
using UniHome.Domain.Accounts;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class RequestPasswordResetServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Request_ConCuentaActivaConfirmada_EmiteTokenHasheadoYEnviaEnlace()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);

        var resets = new InMemoryPasswordResetRepository();
        var sender = new RecordingPasswordResetEmailSender();
        var generator = new FixedPasswordResetTokenGenerator();
        var service = CreateService(accounts, resets, sender, generator);

        var result = await service.RequestAsync(
            new RequestPasswordResetCommand("  ANA@UNI.EDU  "));

        Assert.Equal(RequestPasswordResetStatus.Accepted, result.Status);
        var sent = Assert.Single(sender.Messages);
        Assert.Equal("ana@uni.edu", sent.Email);
        Assert.Equal(generator.Hash(sent.Token), resets.All.Single().TokenHash);
        Assert.NotEqual(sent.Token, resets.All.Single().TokenHash);
        Assert.Equal(account.Id, resets.All.Single().AccountId);
        Assert.Equal(Now.AddMinutes(30), resets.All.Single().ExpiresAt);
    }

    [Fact]
    public async Task Request_ConCorreoInexistente_RespondeIgualSinEnviarNiPersistir()
    {
        var accounts = new InMemoryAccountRepository();
        var resets = new InMemoryPasswordResetRepository();
        var sender = new RecordingPasswordResetEmailSender();
        var service = CreateService(accounts, resets, sender);

        var result = await service.RequestAsync(
            new RequestPasswordResetCommand("nadie@uni.edu"));

        Assert.Equal(RequestPasswordResetStatus.Accepted, result.Status);
        Assert.Empty(sender.Messages);
        Assert.Equal(0, resets.Count);
    }

    [Theory]
    [InlineData(AccountStatus.Suspended, true)]
    [InlineData(AccountStatus.Deleted, true)]
    [InlineData(AccountStatus.Active, false)]
    public async Task Request_ConCuentaNoHabilitada_RespondeIgualSinEnviar(
        AccountStatus status,
        bool emailConfirmed)
    {
        var accounts = new InMemoryAccountRepository();
        await accounts.AddAsync(
            AccountTestData.Create("ana@uni.edu", status: status, emailConfirmed: emailConfirmed));

        var resets = new InMemoryPasswordResetRepository();
        var sender = new RecordingPasswordResetEmailSender();
        var service = CreateService(accounts, resets, sender);

        var result = await service.RequestAsync(
            new RequestPasswordResetCommand("ana@uni.edu"));

        Assert.Equal(RequestPasswordResetStatus.Accepted, result.Status);
        Assert.Empty(sender.Messages);
        Assert.Equal(0, resets.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-correo")]
    public async Task Request_ConCorreoInvalido_DevuelveValidationFailed(string? email)
    {
        var service = CreateService(
            new InMemoryAccountRepository(),
            new InMemoryPasswordResetRepository(),
            new RecordingPasswordResetEmailSender());

        var result = await service.RequestAsync(new RequestPasswordResetCommand(email));

        Assert.Equal(RequestPasswordResetStatus.ValidationFailed, result.Status);
        Assert.Contains("correo", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Request_SiElEnvioFalla_ConservaElTokenYNoCambiaLaRespuesta()
    {
        var accounts = new InMemoryAccountRepository();
        await accounts.AddAsync(AccountTestData.Create("ana@uni.edu"));
        var resets = new InMemoryPasswordResetRepository();
        var service = CreateService(
            accounts,
            resets,
            new ThrowingPasswordResetEmailSender());

        var result = await service.RequestAsync(
            new RequestPasswordResetCommand("ana@uni.edu"));

        Assert.Equal(RequestPasswordResetStatus.Accepted, result.Status);
        Assert.Equal(1, resets.Count);
    }

    [Fact]
    public async Task Request_ConSolicitudAnterior_InvalidaElTokenAnteriorAntesDeEmitirOtro()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);

        var resets = new InMemoryPasswordResetRepository();
        var oldToken = PasswordResetToken.Issue(
            Guid.NewGuid(),
            account.Id,
            "hash:old",
            Now.AddMinutes(-5));
        await resets.AddAsync(oldToken);

        var service = CreateService(
            accounts,
            resets,
            new RecordingPasswordResetEmailSender());

        var result = await service.RequestAsync(
            new RequestPasswordResetCommand("ana@uni.edu"));

        Assert.Equal(RequestPasswordResetStatus.Accepted, result.Status);
        Assert.True(oldToken.IsConsumed);
        Assert.Equal(2, resets.Count);
    }

    [Fact]
    public async Task Request_CuandoElAlmacenamientoFalla_PropagaStorageUnavailable()
    {
        var accounts = new InMemoryAccountRepository();
        await accounts.AddAsync(AccountTestData.Create("ana@uni.edu"));
        var sender = new RecordingPasswordResetEmailSender();
        var service = new RequestPasswordResetService(
            accounts,
            new ThrowingPasswordResetRepository(),
            new FixedPasswordResetTokenGenerator(),
            sender,
            new FixedClock(Now));

        await Assert.ThrowsAsync<UniHome.Application.Identity.Exceptions.StorageUnavailableException>(
            () => service.RequestAsync(new RequestPasswordResetCommand("ana@uni.edu")));

        Assert.Empty(sender.Messages);
    }

    private static RequestPasswordResetService CreateService(
        InMemoryAccountRepository accounts,
        InMemoryPasswordResetRepository resets,
        IPasswordResetEmailSender sender,
        FixedPasswordResetTokenGenerator? generator = null) =>
        new(
            accounts,
            resets,
            generator ?? new FixedPasswordResetTokenGenerator(),
            sender,
            new FixedClock(Now));
}
