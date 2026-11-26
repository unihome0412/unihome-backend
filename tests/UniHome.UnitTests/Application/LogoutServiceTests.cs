using UniHome.Application.Identity.Logout;
using UniHome.Application.Identity.Sessions;
using UniHome.Infrastructure.Redis;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class LogoutServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Logout_RevocaSoloLaSesionIndicada()
    {
        var accountId = Guid.NewGuid();
        var firstSessionId = Guid.NewGuid();
        var secondSessionId = Guid.NewGuid();
        var sessions = new FakeRefreshSessionRepository();

        await sessions.CreateAsync(CreateSession(firstSessionId, accountId));
        await sessions.CreateAsync(CreateSession(secondSessionId, accountId));

        var service = new LogoutService(sessions, new InMemorySessionDenylist(), new FixedAuthTokenSettings(), new FixedClock(Now));

        var revoked = await service.LogoutAsync(accountId, firstSessionId);

        Assert.True(revoked);

        var first = await sessions.FindByIdAsync(firstSessionId);
        var second = await sessions.FindByIdAsync(secondSessionId);

        Assert.NotNull(first);
        Assert.NotNull(first.RevokedAt);
        Assert.Null(second!.RevokedAt);
    }

    [Fact]
    public async Task Logout_DenylisteaElSidRevocadoPeroNoLosDemas()
    {
        var accountId = Guid.NewGuid();
        var firstSessionId = Guid.NewGuid();
        var secondSessionId = Guid.NewGuid();
        var sessions = new FakeRefreshSessionRepository();
        await sessions.CreateAsync(CreateSession(firstSessionId, accountId));
        await sessions.CreateAsync(CreateSession(secondSessionId, accountId));

        var denylist = new InMemorySessionDenylist();
        var service = new LogoutService(sessions, denylist, new FixedAuthTokenSettings(), new FixedClock(Now));

        await service.LogoutAsync(accountId, firstSessionId);

        Assert.True(await denylist.IsDeniedAsync(firstSessionId));
        Assert.False(await denylist.IsDeniedAsync(secondSessionId));
    }

    [Fact]
    public async Task Logout_SesionYaRevocada_NoDenylisteaDeNuevo()
    {
        var accountId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var sessions = new FakeRefreshSessionRepository();
        await sessions.CreateAsync(CreateSession(sessionId, accountId));

        var denylist = new InMemorySessionDenylist();
        var service = new LogoutService(sessions, denylist, new FixedAuthTokenSettings(), new FixedClock(Now));

        await service.LogoutAsync(accountId, sessionId);
        Assert.True(await denylist.IsDeniedAsync(sessionId));

        // La segunda llamada no revoca nada nuevo (RevokeAsync devuelve false);
        // el sid ya estaba denylisteado por la primera, así que el resultado
        // observable es el mismo, sin una segunda escritura necesaria.
        var secondCall = await service.LogoutAsync(accountId, sessionId);
        Assert.False(secondCall);
        Assert.True(await denylist.IsDeniedAsync(sessionId));
    }

    [Fact]
    public async Task Logout_ConCuentaDistinta_NoRevocaLaSesion()
    {
        var accountId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var sessions = new FakeRefreshSessionRepository();
        await sessions.CreateAsync(CreateSession(sessionId, accountId));

        var service = new LogoutService(sessions, new InMemorySessionDenylist(), new FixedAuthTokenSettings(), new FixedClock(Now));

        var revoked = await service.LogoutAsync(Guid.NewGuid(), sessionId);

        Assert.False(revoked);
        Assert.Null((await sessions.FindByIdAsync(sessionId))!.RevokedAt);
    }

    [Fact]
    public async Task Logout_ConIdentificadoresVacios_NoHaceNada()
    {
        var sessions = new FakeRefreshSessionRepository();
        var service = new LogoutService(sessions, new InMemorySessionDenylist(), new FixedAuthTokenSettings(), new FixedClock(Now));

        Assert.False(await service.LogoutAsync(Guid.Empty, Guid.NewGuid()));
        Assert.False(await service.LogoutAsync(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public async Task Logout_Repetido_DevuelveFalso()
    {
        var accountId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var sessions = new FakeRefreshSessionRepository();
        await sessions.CreateAsync(CreateSession(sessionId, accountId));

        var service = new LogoutService(sessions, new InMemorySessionDenylist(), new FixedAuthTokenSettings(), new FixedClock(Now));

        Assert.True(await service.LogoutAsync(accountId, sessionId));
        Assert.False(await service.LogoutAsync(accountId, sessionId));
    }

    private static RefreshSession CreateSession(Guid sessionId, Guid accountId) =>
        new(
            sessionId,
            accountId,
            $"hash:{sessionId:N}",
            Now,
            Now.AddDays(30),
            [],
            RevokedAt: null,
            RevokedReason: null,
            Now,
            Now);
}
