using UniHome.Application.Identity.Logout;
using UniHome.Application.Identity.Sessions;
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

        var service = new LogoutService(sessions, new FixedClock(Now));

        var revoked = await service.LogoutAsync(accountId, firstSessionId);

        Assert.True(revoked);

        var first = await sessions.FindByIdAsync(firstSessionId);
        var second = await sessions.FindByIdAsync(secondSessionId);

        Assert.NotNull(first);
        Assert.NotNull(first.RevokedAt);
        Assert.Null(second!.RevokedAt);
    }

    [Fact]
    public async Task Logout_ConCuentaDistinta_NoRevocaLaSesion()
    {
        var accountId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var sessions = new FakeRefreshSessionRepository();
        await sessions.CreateAsync(CreateSession(sessionId, accountId));

        var service = new LogoutService(sessions, new FixedClock(Now));

        var revoked = await service.LogoutAsync(Guid.NewGuid(), sessionId);

        Assert.False(revoked);
        Assert.Null((await sessions.FindByIdAsync(sessionId))!.RevokedAt);
    }

    [Fact]
    public async Task Logout_ConIdentificadoresVacios_NoHaceNada()
    {
        var sessions = new FakeRefreshSessionRepository();
        var service = new LogoutService(sessions, new FixedClock(Now));

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

        var service = new LogoutService(sessions, new FixedClock(Now));

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
