using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Identity.PasswordRecovery;
using UniHome.Domain.Accounts;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ConfirmPasswordResetServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Confirm_ConTokenValido_CambiaContrasenaConsumeTokenYRevocaSesiones()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);

        var sessions = new FakeRefreshSessionRepository();
        var session = CreateSession(account.Id);
        await sessions.CreateAsync(session);

        var resets = new InMemoryPasswordResetRepository();
        var generator = new FixedPasswordResetTokenGenerator();
        var token = CreatePersistedToken(resets, generator, account.Id, generator.Generate());

        var hasher = new RecordingPasswordHasher();
        var service = CreateService(accounts, resets, generator, hasher, sessions);

        var result = await service.ConfirmAsync(
            new ConfirmPasswordResetCommand(token, "NuevaClave1"));

        Assert.Equal(ConfirmPasswordResetStatus.Success, result.Status);

        var stored = await accounts.FindByIdAsync(account.Id);
        Assert.Equal("hash:NuevaClave1", stored!.PasswordHash);
        Assert.True(resets.All.Single().IsConsumed);
        Assert.NotNull((await sessions.FindByIdAsync(session.Id))!.RevokedAt);
        Assert.Equal("password_reset", (await sessions.FindByIdAsync(session.Id))!.RevokedReason);
        Assert.Equal(1, hasher.HashCalls);
    }

    [Fact]
    public async Task Confirm_ConTokenIncorrecto_DevuelveInvalidoYRegistraIntento()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);

        var resets = new InMemoryPasswordResetRepository();
        var generator = new FixedPasswordResetTokenGenerator();
        var validToken = CreatePersistedToken(resets, generator, account.Id, generator.Generate());
        var wrongToken = validToken[..validToken.IndexOf('.')] + ".BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

        var service = CreateService(
            accounts,
            resets,
            generator,
            new RecordingPasswordHasher(),
            new FakeRefreshSessionRepository());

        var result = await service.ConfirmAsync(
            new ConfirmPasswordResetCommand(wrongToken, "NuevaClave1"));

        Assert.Equal(ConfirmPasswordResetStatus.InvalidToken, result.Status);
        Assert.Equal(1, resets.All.Single().FailedAttempts);
        Assert.Equal("hash:" + AccountTestData.Password, (await accounts.FindByIdAsync(account.Id))!.PasswordHash);
    }

    [Fact]
    public async Task Confirm_ConTokenVencido_DevuelveExpiredToken()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);

        var resets = new InMemoryPasswordResetRepository();
        var generator = new FixedPasswordResetTokenGenerator();
        var generated = generator.Generate();
        var expired = PasswordResetToken.Issue(
            generated.Id,
            account.Id,
            generated.Hash,
            Now.AddMinutes(-31));
        await resets.AddAsync(expired);

        var service = CreateService(
            accounts,
            resets,
            generator,
            new RecordingPasswordHasher(),
            new FakeRefreshSessionRepository());

        var result = await service.ConfirmAsync(
            new ConfirmPasswordResetCommand(generated.Value, "NuevaClave1"));

        Assert.Equal(ConfirmPasswordResetStatus.ExpiredToken, result.Status);
        Assert.False(expired.IsConsumed);
    }

    [Fact]
    public async Task Confirm_ConTokenYaUsado_DevuelveInvalidoSinSegundoCambio()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);

        var resets = new InMemoryPasswordResetRepository();
        var generator = new FixedPasswordResetTokenGenerator();
        var raw = CreatePersistedToken(resets, generator, account.Id, generator.Generate());
        var service = CreateService(
            accounts,
            resets,
            generator,
            new RecordingPasswordHasher(),
            new FakeRefreshSessionRepository());

        var first = await service.ConfirmAsync(
            new ConfirmPasswordResetCommand(raw, "NuevaClave1"));
        var second = await service.ConfirmAsync(
            new ConfirmPasswordResetCommand(raw, "OtraClave2"));

        Assert.Equal(ConfirmPasswordResetStatus.Success, first.Status);
        Assert.Equal(ConfirmPasswordResetStatus.InvalidToken, second.Status);
        Assert.Equal("hash:NuevaClave1", (await accounts.FindByIdAsync(account.Id))!.PasswordHash);
    }

    [Fact]
    public async Task Confirm_TrasAgotarIntentos_DevuelveExpiredToken()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);

        var resets = new InMemoryPasswordResetRepository();
        var generator = new FixedPasswordResetTokenGenerator();
        var validToken = CreatePersistedToken(resets, generator, account.Id, generator.Generate());
        var wrongToken = validToken[..validToken.IndexOf('.')] + ".BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
        var service = CreateService(
            accounts,
            resets,
            generator,
            new RecordingPasswordHasher(),
            new FakeRefreshSessionRepository());

        for (var attempt = 0; attempt < PasswordResetToken.MaxAttempts; attempt++)
        {
            var result = await service.ConfirmAsync(
                new ConfirmPasswordResetCommand(wrongToken, "NuevaClave1"));

            if (attempt < PasswordResetToken.MaxAttempts - 1)
            {
                Assert.Equal(ConfirmPasswordResetStatus.InvalidToken, result.Status);
            }
            else
            {
                Assert.Equal(ConfirmPasswordResetStatus.ExpiredToken, result.Status);
            }
        }
    }

    [Theory]
    [InlineData(null, "NuevaClave1", "token")]
    [InlineData("token-invalido", "corta", "nuevaContrasena")]
    [InlineData("token-invalido", null, "nuevaContrasena")]
    public async Task Confirm_ConDatosInvalidos_DevuelveValidationFailed(
        string? token,
        string? newPassword,
        string expectedField)
    {
        var service = CreateService(
            new InMemoryAccountRepository(),
            new InMemoryPasswordResetRepository(),
            new FixedPasswordResetTokenGenerator(),
            new RecordingPasswordHasher(),
            new FakeRefreshSessionRepository());

        var result = await service.ConfirmAsync(
            new ConfirmPasswordResetCommand(token, newPassword));

        Assert.Equal(ConfirmPasswordResetStatus.ValidationFailed, result.Status);
        Assert.Contains(expectedField, result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Confirm_ConTokenMalFormado_DevuelveInvalidToken()
    {
        var service = CreateService(
            new InMemoryAccountRepository(),
            new InMemoryPasswordResetRepository(),
            new FixedPasswordResetTokenGenerator(),
            new RecordingPasswordHasher(),
            new FakeRefreshSessionRepository());

        var result = await service.ConfirmAsync(
            new ConfirmPasswordResetCommand("token-invalido", "NuevaClave1"));

        Assert.Equal(ConfirmPasswordResetStatus.InvalidToken, result.Status);
    }

    [Fact]
    public async Task Confirm_ConCuentaSuspendida_DevuelveInvalidoSinConsumir()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu", status: AccountStatus.Suspended);
        await accounts.AddAsync(account);

        var resets = new InMemoryPasswordResetRepository();
        var generator = new FixedPasswordResetTokenGenerator();
        var raw = CreatePersistedToken(resets, generator, account.Id, generator.Generate());
        var service = CreateService(
            accounts,
            resets,
            generator,
            new RecordingPasswordHasher(),
            new FakeRefreshSessionRepository());

        var result = await service.ConfirmAsync(
            new ConfirmPasswordResetCommand(raw, "NuevaClave1"));

        Assert.Equal(ConfirmPasswordResetStatus.InvalidToken, result.Status);
        Assert.False(resets.All.Single().IsConsumed);
    }

    [Fact]
    public async Task Confirm_DosSolicitudesConcurrentes_SoloUnaCambiaLaContrasena()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);

        var resets = new InMemoryPasswordResetRepository();
        var generator = new FixedPasswordResetTokenGenerator();
        var raw = CreatePersistedToken(resets, generator, account.Id, generator.Generate());
        var service = CreateService(
            accounts,
            resets,
            generator,
            new RecordingPasswordHasher(),
            new FakeRefreshSessionRepository());

        var results = await Task.WhenAll(
            service.ConfirmAsync(new ConfirmPasswordResetCommand(raw, "NuevaClave1")),
            service.ConfirmAsync(new ConfirmPasswordResetCommand(raw, "NuevaClave2")));

        Assert.Single(results, result => result.Status == ConfirmPasswordResetStatus.Success);
        Assert.Single(results, result => result.Status == ConfirmPasswordResetStatus.InvalidToken);
        Assert.True(resets.All.Single().IsConsumed);
    }

    [Fact]
    public async Task Confirm_DosTokensDistintosConcurrentes_SoloUnCambioDeHashGana()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);
        var oldHash = account.PasswordHash;

        var results = await Task.WhenAll(
            accounts.TryChangePasswordAsync(account.Id, oldHash, "hash:NuevaClave1"),
            accounts.TryChangePasswordAsync(account.Id, oldHash, "hash:NuevaClave2"));

        Assert.Single(results, result => result);
        Assert.Single(results, result => !result);
        var stored = await accounts.FindByIdAsync(account.Id);
        Assert.True(stored!.PasswordHash is "hash:NuevaClave1" or "hash:NuevaClave2");
    }

    [Fact]
    public async Task Confirm_CuandoFallaElCambioDeContrasena_NoCambiaLaContrasenaYConsumeElToken()
    {
        var innerAccounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await innerAccounts.AddAsync(account);
        var accounts = new FlakyAccountRepository(innerAccounts);
        accounts.FailNextPasswordChange();

        var resets = new InMemoryPasswordResetRepository();
        var generator = new FixedPasswordResetTokenGenerator();
        var raw = CreatePersistedToken(resets, generator, account.Id, generator.Generate());
        var sessions = new FakeRefreshSessionRepository();
        var session = CreateSession(account.Id);
        await sessions.CreateAsync(session);

        var service = CreateService(
            accounts,
            resets,
            generator,
            new RecordingPasswordHasher(),
            sessions);

        await Assert.ThrowsAsync<StorageUnavailableException>(() =>
            service.ConfirmAsync(new ConfirmPasswordResetCommand(raw, "NuevaClave1")));

        var stored = await innerAccounts.FindByIdAsync(account.Id);
        Assert.Equal("hash:" + AccountTestData.Password, stored!.PasswordHash);
        Assert.True(resets.All.Single().IsConsumed);
        Assert.NotNull((await sessions.FindByIdAsync(session.Id))!.RevokedAt);
    }

    [Fact]
    public async Task Confirm_CuandoFallaRevocarSesiones_NoCambiaLaContrasenaYConsumeElToken()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);

        var resets = new InMemoryPasswordResetRepository();
        var generator = new FixedPasswordResetTokenGenerator();
        var raw = CreatePersistedToken(resets, generator, account.Id, generator.Generate());
        var innerSessions = new FakeRefreshSessionRepository();
        var session = CreateSession(account.Id);
        await innerSessions.CreateAsync(session);
        var sessions = new FailingSessionRevocationRepository(innerSessions);

        var service = CreateService(
            accounts,
            resets,
            generator,
            new RecordingPasswordHasher(),
            sessions);

        await Assert.ThrowsAsync<StorageUnavailableException>(() =>
            service.ConfirmAsync(new ConfirmPasswordResetCommand(raw, "NuevaClave1")));

        var stored = await accounts.FindByIdAsync(account.Id);
        Assert.Equal("hash:" + AccountTestData.Password, stored!.PasswordHash);
        Assert.True(resets.All.Single().IsConsumed);
        Assert.Null((await innerSessions.FindByIdAsync(session.Id))!.RevokedAt);
    }

    [Fact]
    public async Task Confirm_TrasFalloDeActualizacion_PermiteSolicitarOtroEnlaceYCompletar()
    {
        var innerAccounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await innerAccounts.AddAsync(account);
        var accounts = new FlakyAccountRepository(innerAccounts);
        accounts.FailNextPasswordChange();

        var resets = new InMemoryPasswordResetRepository();
        var generator = new FixedPasswordResetTokenGenerator();
        var sender = new RecordingPasswordResetEmailSender();
        var sessions = new FakeRefreshSessionRepository();
        var session = CreateSession(account.Id);
        await sessions.CreateAsync(session);

        var confirm = CreateService(
            accounts,
            resets,
            generator,
            new RecordingPasswordHasher(),
            sessions);
        var firstToken = CreatePersistedToken(
            resets,
            generator,
            account.Id,
            generator.Generate());

        await Assert.ThrowsAsync<StorageUnavailableException>(() =>
            confirm.ConfirmAsync(new ConfirmPasswordResetCommand(firstToken, "NuevaClave1")));

        var request = new RequestPasswordResetService(
            accounts,
            resets,
            generator,
            sender,
            new FixedClock(Now));
        var requested = await request.RequestAsync(
            new RequestPasswordResetCommand("ana@uni.edu"));

        Assert.Equal(RequestPasswordResetStatus.Accepted, requested.Status);
        var secondToken = Assert.Single(sender.Messages).Token;

        var result = await confirm.ConfirmAsync(
            new ConfirmPasswordResetCommand(secondToken, "NuevaClave2"));

        Assert.Equal(ConfirmPasswordResetStatus.Success, result.Status);
        var stored = await innerAccounts.FindByIdAsync(account.Id);
        Assert.Equal("hash:NuevaClave2", stored!.PasswordHash);
    }

    private static string CreatePersistedToken(
        InMemoryPasswordResetRepository resets,
        FixedPasswordResetTokenGenerator generator,
        Guid accountId,
        UniHome.Application.Identity.Abstractions.GeneratedPasswordResetToken generated)
    {
        var token = PasswordResetToken.Issue(
            generated.Id,
            accountId,
            generator.Hash(generated.Value),
            Now);
        resets.AddAsync(token).GetAwaiter().GetResult();
        return generated.Value;
    }

    private static ConfirmPasswordResetService CreateService(
        IAccountRepository accounts,
        InMemoryPasswordResetRepository resets,
        FixedPasswordResetTokenGenerator generator,
        RecordingPasswordHasher hasher,
        IRefreshSessionRepository sessions) =>
        new(
            accounts,
            resets,
            generator,
            hasher,
            sessions,
            new FixedClock(Now));

    private static UniHome.Application.Identity.Sessions.RefreshSession CreateSession(Guid accountId) =>
        new(
            Guid.NewGuid(),
            accountId,
            "hash:refresh",
            Now,
            Now.AddDays(30),
            [],
            RevokedAt: null,
            RevokedReason: null,
            Now,
            Now);
}
