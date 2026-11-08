using UniHome.Application.Identity.Login;
using UniHome.Domain.Accounts;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class LoginServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Login_ConComandoNulo_LanzaArgumentNullException()
    {
        var service = CreateService(
            new InMemoryAccountRepository(),
            new RecordingPasswordHasher(),
            new FakeRefreshSessionRepository());

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.LoginAsync(null!));
    }

    [Fact]
    public async Task Login_ConCredencialesValidas_CreaSesionYEmiteTokens()
    {
        var repository = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await repository.AddAsync(account);

        var hasher = new RecordingPasswordHasher();
        var sessions = new FakeRefreshSessionRepository();
        var generator = new FakeRefreshTokenGenerator();
        var issuer = new FakeAccessTokenIssuer();
        var service = CreateService(repository, hasher, sessions, generator, issuer);

        var result = await service.LoginAsync(
            new LoginCommand("  ANA@UNI.EDU  ", AccountTestData.Password));

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal(Now.AddMinutes(60), result.ExpiresAt);

        var session = sessions.Single();
        Assert.Equal(account.Id, session.AccountId);
        Assert.Equal(generator.Hash(result.RefreshToken!), session.CurrentTokenHash);
        Assert.Equal(Now, session.CurrentTokenIssuedAt);
        Assert.Equal(Now.AddDays(30), session.ExpiresAt);
        Assert.Empty(session.UsedTokens);
        Assert.Null(session.RevokedAt);

        var issue = Assert.Single(issuer.Calls);
        Assert.Equal(account.Id, issue.AccountId);
        Assert.Equal(session.Id, issue.SessionId);
    }

    [Fact]
    public async Task Login_ConContrasenaIncorrecta_DevuelveCredencialesInvalidasSinSesion()
    {
        var repository = new InMemoryAccountRepository();
        await repository.AddAsync(AccountTestData.Create("ana@uni.edu"));

        var sessions = new FakeRefreshSessionRepository();
        var service = CreateService(repository, new RecordingPasswordHasher(), sessions);

        var result = await service.LoginAsync(
            new LoginCommand("ana@uni.edu", "OtraClave1"));

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.AccessToken);
        Assert.Null(result.RefreshToken);
        Assert.Equal(0, sessions.Count);
    }

    [Fact]
    public async Task Login_ConCorreoInexistente_VerificaContraHashFicticio()
    {
        var repository = new InMemoryAccountRepository();
        var hasher = new RecordingPasswordHasher();
        var sessions = new FakeRefreshSessionRepository();
        var service = CreateService(repository, hasher, sessions);

        var result = await service.LoginAsync(
            new LoginCommand("nadie@uni.edu", "OtraClave1"));

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        var verification = Assert.Single(hasher.VerifyCalls);
        Assert.Equal("hash:dummy", verification.PasswordHash);
        Assert.Equal(0, sessions.Count);
    }

    [Fact]
    public async Task Login_ConCorreoNoConfirmado_DevuelveEstadoEspecifico()
    {
        var repository = new InMemoryAccountRepository();
        await repository.AddAsync(AccountTestData.Create("ana@uni.edu", emailConfirmed: false));

        var service = CreateService(repository, new RecordingPasswordHasher(), new FakeRefreshSessionRepository());

        var result = await service.LoginAsync(
            new LoginCommand("ana@uni.edu", AccountTestData.Password));

        Assert.Equal(LoginStatus.EmailNotConfirmed, result.Status);
        Assert.Null(result.AccessToken);
    }

    [Fact]
    public async Task Login_ConCuentaSuspendida_DevuelveSuspendida()
    {
        var repository = new InMemoryAccountRepository();
        await repository.AddAsync(
            AccountTestData.Create("ana@uni.edu", status: AccountStatus.Suspended));

        var service = CreateService(repository, new RecordingPasswordHasher(), new FakeRefreshSessionRepository());

        var result = await service.LoginAsync(
            new LoginCommand("ana@uni.edu", AccountTestData.Password));

        Assert.Equal(LoginStatus.Suspended, result.Status);
    }

    [Fact]
    public async Task Login_ConCuentaEliminada_NoEmiteTokens()
    {
        var repository = new InMemoryAccountRepository();
        await repository.AddAsync(
            AccountTestData.Create("ana@uni.edu", status: AccountStatus.Deleted));

        var service = CreateService(repository, new RecordingPasswordHasher(), new FakeRefreshSessionRepository());

        var result = await service.LoginAsync(
            new LoginCommand("ana@uni.edu", AccountTestData.Password));

        Assert.Equal(LoginStatus.Deleted, result.Status);
        Assert.Null(result.AccessToken);
    }

    [Theory]
    [InlineData("", AccountTestData.Password, "correo", "El correo electrónico es obligatorio.")]
    [InlineData("ana@uni.edu", "", "contrasena", "La contraseña es obligatoria.")]
    [InlineData("no-es-correo", AccountTestData.Password, "correo", "El correo electrónico no tiene un formato válido.")]
    public async Task Login_ConDatosInvalidos_NoCreaSesion(
        string email,
        string password,
        string expectedField,
        string expectedMessage)
    {
        var repository = new InMemoryAccountRepository();
        var sessions = new FakeRefreshSessionRepository();
        var service = CreateService(repository, new RecordingPasswordHasher(), sessions);

        var result = await service.LoginAsync(new LoginCommand(email, password));

        Assert.Equal(LoginStatus.ValidationFailed, result.Status);
        var error = Assert.Single(result.ValidationErrors);
        Assert.Equal(expectedField, error.Key);
        Assert.Equal(new[] { expectedMessage }, error.Value);
        Assert.Equal(0, sessions.Count);
    }

    [Fact]
    public async Task Login_ConContrasenaDemasiadoLarga_NoInvocaElHasher()
    {
        var repository = new InMemoryAccountRepository();
        var hasher = new RecordingPasswordHasher();
        var service = CreateService(repository, hasher, new FakeRefreshSessionRepository());

        var result = await service.LoginAsync(
            new LoginCommand("ana@uni.edu", new string('x', 129)));

        Assert.Equal(LoginStatus.ValidationFailed, result.Status);
        var error = Assert.Single(result.ValidationErrors);
        Assert.Equal("contrasena", error.Key);
        Assert.Equal(
            new[] { "La contraseña no debe superar 128 caracteres." },
            error.Value);
        Assert.Empty(hasher.VerifyCalls);
    }

    [Fact]
    public async Task Login_ConContrasenaDeLongitudMaxima_NoFallaValidacion()
    {
        var repository = new InMemoryAccountRepository();
        await repository.AddAsync(AccountTestData.Create("ana@uni.edu"));
        var hasher = new RecordingPasswordHasher();
        var service = CreateService(repository, hasher, new FakeRefreshSessionRepository());
        var password = new string('x', 128);

        var result = await service.LoginAsync(
            new LoginCommand("ana@uni.edu", password));

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        var verification = Assert.Single(hasher.VerifyCalls);
        Assert.Equal(password, verification.Password);
    }

    private static LoginService CreateService(
        InMemoryAccountRepository repository,
        RecordingPasswordHasher hasher,
        FakeRefreshSessionRepository sessions,
        FakeRefreshTokenGenerator? generator = null,
        FakeAccessTokenIssuer? issuer = null) =>
        new(
            repository,
            hasher,
            new FixedDummyPasswordHashProvider(),
            new FixedClock(Now),
            sessions,
            generator ?? new FakeRefreshTokenGenerator(),
            issuer ?? new FakeAccessTokenIssuer(),
            new FixedAuthTokenSettings());
}
