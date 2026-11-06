using UniHome.Application.Identity.RefreshSessions;
using UniHome.Application.Identity.Sessions;
using UniHome.Domain.Accounts;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class RefreshSessionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Refresh_ConTokenVigente_RotaElTokenYEmiteNuevosAccesos()
    {
        var context = await CreateContextAsync();

        var result = await context.Service.RefreshAsync(
            new RefreshSessionCommand(context.InitialToken));

        Assert.Equal(RefreshSessionStatus.Success, result.Status);
        Assert.NotEqual(context.InitialToken, result.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));

        var session = await context.Sessions.FindByIdAsync(context.SessionId);
        Assert.NotNull(session);
        Assert.Equal(context.Generator.Hash(result.RefreshToken!), session.CurrentTokenHash);
        Assert.Equal(Now.AddDays(30), session.ExpiresAt);
        Assert.Contains(
            session.UsedTokens,
            used => used.TokenHash == context.Generator.Hash(context.InitialToken));
    }

    [Fact]
    public async Task Refresh_ConTokenReutilizado_RevocaLaFamilia()
    {
        var context = await CreateContextAsync();
        var first = await context.Service.RefreshAsync(
            new RefreshSessionCommand(context.InitialToken));
        Assert.Equal(RefreshSessionStatus.Success, first.Status);

        var reuse = await context.Service.RefreshAsync(
            new RefreshSessionCommand(context.InitialToken));

        Assert.Equal(RefreshSessionStatus.Invalid, reuse.Status);

        var session = await context.Sessions.FindByIdAsync(context.SessionId);
        Assert.NotNull(session);
        Assert.NotNull(session.RevokedAt);

        var afterRevocation = await context.Service.RefreshAsync(
            new RefreshSessionCommand(first.RefreshToken!));
        Assert.Equal(RefreshSessionStatus.Invalid, afterRevocation.Status);
    }

    [Fact]
    public async Task Refresh_ConSesionVencida_DevuelveInvalido()
    {
        var context = await CreateContextAsync();
        context.Sessions.SetExpiresAt(context.SessionId, Now.AddMinutes(-1));

        var result = await context.Service.RefreshAsync(
            new RefreshSessionCommand(context.InitialToken));

        Assert.Equal(RefreshSessionStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Refresh_ConSesionRevocada_DevuelveInvalido()
    {
        var context = await CreateContextAsync();
        await context.Sessions.RevokeAsync(
            context.SessionId,
            context.Account.Id,
            Now,
            "test");

        var result = await context.Service.RefreshAsync(
            new RefreshSessionCommand(context.InitialToken));

        Assert.Equal(RefreshSessionStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Refresh_ConCuentaSuspendida_RevocaLaSesion()
    {
        var context = await CreateContextAsync();
        await context.Accounts.UpdateAsync(
            AccountTestData.WithStatus(context.Account, AccountStatus.Suspended));

        var result = await context.Service.RefreshAsync(
            new RefreshSessionCommand(context.InitialToken));

        Assert.Equal(RefreshSessionStatus.Invalid, result.Status);

        var session = await context.Sessions.FindByIdAsync(context.SessionId);
        Assert.NotNull(session);
        Assert.NotNull(session.RevokedAt);
    }

    [Fact]
    public async Task Refresh_ConCuentaEliminada_RevocaLaSesion()
    {
        var context = await CreateContextAsync();
        await context.Accounts.UpdateAsync(
            AccountTestData.WithStatus(context.Account, AccountStatus.Deleted));

        var result = await context.Service.RefreshAsync(
            new RefreshSessionCommand(context.InitialToken));

        Assert.Equal(RefreshSessionStatus.Invalid, result.Status);

        var session = await context.Sessions.FindByIdAsync(context.SessionId);
        Assert.NotNull(session);
        Assert.NotNull(session.RevokedAt);
    }

    [Fact]
    public async Task Refresh_SinToken_DevuelveErrorDeValidacion()
    {
        var context = await CreateContextAsync();

        var result = await context.Service.RefreshAsync(
            new RefreshSessionCommand(null));

        Assert.Equal(RefreshSessionStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors.ContainsKey("refreshToken"));
    }

    [Fact]
    public async Task Refresh_ConTokenMalformado_DevuelveInvalido()
    {
        var context = await CreateContextAsync();

        var result = await context.Service.RefreshAsync(
            new RefreshSessionCommand("no-es-un-token"));

        Assert.Equal(RefreshSessionStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Refresh_Concurrente_ConElMismoToken_SoloUnaOperacionRota()
    {
        var context = await CreateContextAsync();

        var first = context.Service.RefreshAsync(
            new RefreshSessionCommand(context.InitialToken));
        var second = context.Service.RefreshAsync(
            new RefreshSessionCommand(context.InitialToken));

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, results.Count(result => result.Status == RefreshSessionStatus.Success));
        Assert.Equal(1, results.Count(result => result.Status == RefreshSessionStatus.Invalid));

        var session = await context.Sessions.FindByIdAsync(context.SessionId);
        Assert.NotNull(session);
        Assert.NotNull(session.RevokedAt);
    }

    private static async Task<TestContext> CreateContextAsync()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);

        var sessions = new FakeRefreshSessionRepository();
        var generator = new FakeRefreshTokenGenerator();
        var issuer = new FakeAccessTokenIssuer();
        var clock = new FixedClock(Now);
        var sessionId = Guid.NewGuid();
        var token = generator.Generate(sessionId);

        var session = new RefreshSession(
            sessionId,
            account.Id,
            token.Hash,
            Now,
            Now.AddDays(30),
            [],
            RevokedAt: null,
            RevokedReason: null,
            Now,
            Now);

        await sessions.CreateAsync(session);

        var service = new RefreshSessionService(
            sessions,
            accounts,
            generator,
            issuer,
            new FixedAuthTokenSettings(),
            clock);

        return new TestContext(
            service,
            accounts,
            sessions,
            generator,
            account,
            sessionId,
            token.Value);
    }

    private sealed record TestContext(
        RefreshSessionService Service,
        InMemoryAccountRepository Accounts,
        FakeRefreshSessionRepository Sessions,
        FakeRefreshTokenGenerator Generator,
        Account Account,
        Guid SessionId,
        string InitialToken);
}
