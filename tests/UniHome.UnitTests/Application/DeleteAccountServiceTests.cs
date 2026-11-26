using UniHome.Application.Identity.DeleteAccount;
using UniHome.Domain.Accounts;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.Infrastructure.Redis;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class DeleteAccountServiceTests
{
    private const string Password = "ClaveSegura1";
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Delete_ConContrasenaCorrectaYConfirmacion_EliminaYRevocaSesiones()
    {
        var repository = new InMemoryAccountRepository();
        var sessions = new FakeRefreshSessionRepository();
        var account = CreateActiveAccount("ana@uni.edu");
        await repository.AddAsync(account);

        var firstSession = CreateSession(Guid.NewGuid(), account.Id);
        var secondSession = CreateSession(Guid.NewGuid(), account.Id);
        var otherAccountSession = CreateSession(Guid.NewGuid(), Guid.NewGuid());
        await sessions.CreateAsync(firstSession);
        await sessions.CreateAsync(secondSession);
        await sessions.CreateAsync(otherAccountSession);

        var denylist = new InMemorySessionDenylist();
        var service = CreateService(repository, sessions, denylist: denylist);
        var result = await service.DeleteAsync(
            new DeleteAccountCommand(account.Id, Password, ConfirmedDataLoss: true));

        Assert.True(result.Succeeded);
        Assert.Equal(DeleteAccountStatus.Deleted, result.Status);

        var stored = await repository.FindByIdAsync(account.Id);
        Assert.Equal(AccountStatus.Deleted, stored!.Status);
        Assert.Equal(Now, stored.DeletedAt);

        Assert.NotNull((await sessions.FindByIdAsync(firstSession.Id))!.RevokedAt);
        Assert.NotNull((await sessions.FindByIdAsync(secondSession.Id))!.RevokedAt);
        Assert.Null((await sessions.FindByIdAsync(otherAccountSession.Id))!.RevokedAt);

        Assert.True(await denylist.IsDeniedAsync(firstSession.Id));
        Assert.True(await denylist.IsDeniedAsync(secondSession.Id));
        Assert.False(
            await denylist.IsDeniedAsync(otherAccountSession.Id),
            "Solo deben denylistearse los sid de la cuenta eliminada.");
    }

    [Fact]
    public async Task Delete_ConContrasenaIncorrecta_DevuelveIncorrectPasswordSinMutarNada()
    {
        var repository = new InMemoryAccountRepository();
        var sessions = new FakeRefreshSessionRepository();
        var account = CreateActiveAccount("ana@uni.edu");
        await repository.AddAsync(account);
        var session = CreateSession(Guid.NewGuid(), account.Id);
        await sessions.CreateAsync(session);

        var service = CreateService(repository, sessions);
        var result = await service.DeleteAsync(
            new DeleteAccountCommand(account.Id, "OtraClave1", ConfirmedDataLoss: true));

        Assert.Equal(DeleteAccountStatus.IncorrectPassword, result.Status);

        var stored = await repository.FindByIdAsync(account.Id);
        Assert.Equal(AccountStatus.Active, stored!.Status);
        Assert.Null((await sessions.FindByIdAsync(session.Id))!.RevokedAt);
    }

    [Fact]
    public async Task Delete_SinConfirmacion_DevuelveConfirmationMissingSinComprobarContrasena()
    {
        var repository = new InMemoryAccountRepository();
        var sessions = new FakeRefreshSessionRepository();
        var account = CreateActiveAccount("ana@uni.edu");
        await repository.AddAsync(account);

        var hasher = new RecordingPasswordHasher();
        var service = new DeleteAccountService(
            repository,
            hasher,
            sessions,
            new InMemorySessionDenylist(),
            new FixedAuthTokenSettings(),
            new FakeOccupancyLinkRepository(),
            new FakeUnitRepository(),
            new FakeAdminAccessChecker(),
            new FixedClock(Now));

        var result = await service.DeleteAsync(
            new DeleteAccountCommand(account.Id, Password, ConfirmedDataLoss: false));

        Assert.Equal(DeleteAccountStatus.ConfirmationMissing, result.Status);
        Assert.Empty(hasher.VerifyCalls);

        var stored = await repository.FindByIdAsync(account.Id);
        Assert.Equal(AccountStatus.Active, stored!.Status);
    }

    [Fact]
    public async Task Delete_SobreCuentaYaEliminada_DevuelveAccountNotFound()
    {
        var repository = new InMemoryAccountRepository();
        var sessions = new FakeRefreshSessionRepository();
        var account = CreateActiveAccount("ana@uni.edu");
        account.Delete(Now);
        await repository.AddAsync(account);

        var service = CreateService(repository, sessions);
        var result = await service.DeleteAsync(
            new DeleteAccountCommand(account.Id, Password, ConfirmedDataLoss: true));

        Assert.Equal(DeleteAccountStatus.AccountNotFound, result.Status);
    }

    [Fact]
    public async Task Delete_ConCuentaInexistente_DevuelveAccountNotFound()
    {
        var repository = new InMemoryAccountRepository();
        var sessions = new FakeRefreshSessionRepository();
        var service = CreateService(repository, sessions);

        var result = await service.DeleteAsync(
            new DeleteAccountCommand(Guid.NewGuid(), Password, ConfirmedDataLoss: true));

        Assert.Equal(DeleteAccountStatus.AccountNotFound, result.Status);
    }

    [Fact]
    public async Task Delete_ConVinculoDeOcupacionActivo_DevuelveActiveOccupancyLinksSinEliminar()
    {
        var repository = new InMemoryAccountRepository();
        var sessions = new FakeRefreshSessionRepository();
        var account = CreateActiveAccount("inquilina@uni.edu");
        await repository.AddAsync(account);

        var occupancyLinks = new FakeOccupancyLinkRepository();
        occupancyLinks.Items.Add(OccupancyLink.CreateActive(account.Id, Guid.NewGuid(), Now));

        var service = CreateService(repository, sessions, occupancyLinks: occupancyLinks);
        var result = await service.DeleteAsync(
            new DeleteAccountCommand(account.Id, Password, ConfirmedDataLoss: true));

        Assert.False(result.Succeeded);
        Assert.Equal(DeleteAccountStatus.ActiveOccupancyLinks, result.Status);

        var stored = await repository.FindByIdAsync(account.Id);
        Assert.Equal(AccountStatus.Active, stored!.Status);
    }

    [Fact]
    public async Task Delete_ConPropiedadConInquilinosActivos_DevuelvePropertiesWithTenantsSinEliminar()
    {
        var repository = new InMemoryAccountRepository();
        var sessions = new FakeRefreshSessionRepository();
        var account = CreateActiveAccount("propietaria@uni.edu");
        await repository.AddAsync(account);

        var unit = Unit.Restore(
            Guid.NewGuid(),
            Guid.NewGuid(),
            account.Id,
            Guid.NewGuid(),
            UnitType.Habitacion,
            Occupancy.Compartida,
            3,
            1500m,
            12m,
            ForWhom.Mixto,
            BathroomType.Compartido,
            FurnitureType.SinMuebles,
            furnitureItems: null,
            template: null,
            zoneInfo: null,
            rules: null,
            services: null,
            photos: null,
            PublicationState.Aprobada,
            Now);

        var units = new FakeUnitRepository();
        units.Items.Add(unit);

        var occupancyLinks = new FakeOccupancyLinkRepository();
        occupancyLinks.Items.Add(OccupancyLink.CreateActive(Guid.NewGuid(), unit.Id, Now));

        var service = CreateService(repository, sessions, occupancyLinks: occupancyLinks, units: units);
        var result = await service.DeleteAsync(
            new DeleteAccountCommand(account.Id, Password, ConfirmedDataLoss: true));

        Assert.False(result.Succeeded);
        Assert.Equal(DeleteAccountStatus.PropertiesWithTenants, result.Status);

        var stored = await repository.FindByIdAsync(account.Id);
        Assert.Equal(AccountStatus.Active, stored!.Status);
    }

    [Fact]
    public async Task Delete_ConCuentaAdministrativa_DevuelveAdminCannotSelfDeleteSinEliminar()
    {
        var repository = new InMemoryAccountRepository();
        var sessions = new FakeRefreshSessionRepository();
        var account = CreateActiveAccount("admin@uni.edu");
        await repository.AddAsync(account);

        var adminAccess = new FakeAdminAccessChecker();
        adminAccess.AdminAccountIds.Add(account.Id);

        var service = CreateService(repository, sessions, adminAccess: adminAccess);
        var result = await service.DeleteAsync(
            new DeleteAccountCommand(account.Id, Password, ConfirmedDataLoss: true));

        Assert.False(result.Succeeded);
        Assert.Equal(DeleteAccountStatus.AdminCannotSelfDelete, result.Status);

        var stored = await repository.FindByIdAsync(account.Id);
        Assert.Equal(AccountStatus.Active, stored!.Status);
    }

    private static DeleteAccountService CreateService(
        InMemoryAccountRepository repository,
        FakeRefreshSessionRepository sessions,
        FakeOccupancyLinkRepository? occupancyLinks = null,
        FakeUnitRepository? units = null,
        FakeAdminAccessChecker? adminAccess = null,
        InMemorySessionDenylist? denylist = null) =>
        new(
            repository,
            new RecordingPasswordHasher(),
            sessions,
            denylist ?? new InMemorySessionDenylist(),
            new FixedAuthTokenSettings(),
            occupancyLinks ?? new FakeOccupancyLinkRepository(),
            units ?? new FakeUnitRepository(),
            adminAccess ?? new FakeAdminAccessChecker(),
            new FixedClock(Now));

    private static Account CreateActiveAccount(string email) =>
        Account.Restore(
            Guid.NewGuid(),
            "Ana López",
            EmailAddress.Create(email),
            new DateOnly(2004, 5, 10),
            Gender.Mujer,
            $"hash:{Password}",
            new PrivacyAcceptance("2026-01", Now),
            AccountStatus.Active,
            isEmailConfirmed: true,
            Now,
            Now,
            emailVerification: null,
            verificationResendAttempts: []);

    private static UniHome.Application.Identity.Sessions.RefreshSession CreateSession(
        Guid sessionId, Guid accountId) =>
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
