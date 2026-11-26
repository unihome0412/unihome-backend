using UniHome.Application.Common;
using UniHome.Application.Identity.Admin;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Redis;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class SetAccountStatusServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SetStatusAsync_Suspender_CambiaEstadoYRevocaTodasLasSesiones()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);
        var sessions = new FakeRefreshSessionRepository();
        var session = CreateSession(account.Id);
        await sessions.CreateAsync(session);
        var denylist = new InMemorySessionDenylist();
        var service = new SetAccountStatusService(
            accounts, sessions, denylist, new FixedAuthTokenSettings(), new FixedClock(Now));

        var result = await service.SetStatusAsync(account.Id, suspender: true);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(AccountStatus.Suspended, result.Value!.Status);
        Assert.NotNull((await sessions.FindByIdAsync(session.Id))!.RevokedAt);
        Assert.True(
            await denylist.IsDeniedAsync(session.Id),
            "Suspender debe denylistear el sid de cada sesión revocada, no solo marcarla revocada en Mongo.");
    }

    [Fact]
    public async Task SetStatusAsync_Reactivar_CambiaEstadoSinRevocarSesiones()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("suspendida@uni.edu", status: AccountStatus.Suspended);
        await accounts.AddAsync(account);
        var sessions = new FakeRefreshSessionRepository();
        var service = new SetAccountStatusService(
            accounts, sessions, new InMemorySessionDenylist(), new FixedAuthTokenSettings(), new FixedClock(Now));

        var result = await service.SetStatusAsync(account.Id, suspender: false);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(AccountStatus.Active, result.Value!.Status);
    }

    [Fact]
    public async Task SetStatusAsync_CuentaInexistente_DevuelveNotFound()
    {
        var service = new SetAccountStatusService(
            new InMemoryAccountRepository(), new FakeRefreshSessionRepository(), new InMemorySessionDenylist(), new FixedAuthTokenSettings(), new FixedClock(Now));

        var result = await service.SetStatusAsync(Guid.NewGuid(), suspender: true);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SetStatusAsync_SuspenderUnaCuentaYaSuspendida_DevuelveValidationFailed()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu", status: AccountStatus.Suspended);
        await accounts.AddAsync(account);
        var service = new SetAccountStatusService(
            accounts, new FakeRefreshSessionRepository(), new InMemorySessionDenylist(), new FixedAuthTokenSettings(), new FixedClock(Now));

        var result = await service.SetStatusAsync(account.Id, suspender: true);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task SetStatusAsync_ReactivarUnaCuentaEliminada_DevuelveValidationFailed()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        account.Delete(Now);
        await accounts.AddAsync(account);
        var service = new SetAccountStatusService(
            accounts, new FakeRefreshSessionRepository(), new InMemorySessionDenylist(), new FixedAuthTokenSettings(), new FixedClock(Now));

        var result = await service.SetStatusAsync(account.Id, suspender: false);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    private static UniHome.Application.Identity.Sessions.RefreshSession CreateSession(Guid accountId) =>
        new(
            Guid.NewGuid(),
            accountId,
            "hash:session",
            Now,
            Now.AddDays(30),
            [],
            RevokedAt: null,
            RevokedReason: null,
            Now,
            Now);
}
