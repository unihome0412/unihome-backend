using UniHome.Application.Common;
using UniHome.Application.Profiles;
using UniHome.Domain.Accounts;
using UniHome.Domain.Universities;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ChangeMyUniversityServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ChangeAsync_ConUniversidadActiva_LaFijaEnLaCuenta()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);
        var universities = new FakeUniversityRepository();
        var university = University.Restore(Guid.NewGuid(), "Universidad X", true, Now);
        universities.Items.Add(university);
        var service = new ChangeMyUniversityService(accounts, universities);

        var result = await service.ChangeAsync(account.Id, university.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(university.Id, result.Value!.CurrentUniversityId);
    }

    [Fact]
    public async Task ChangeAsync_ConUniversidadInexistente_DevuelveValidationFailed()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);
        var service = new ChangeMyUniversityService(accounts, new FakeUniversityRepository());

        var result = await service.ChangeAsync(account.Id, Guid.NewGuid());

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task ChangeAsync_ConUniversidadInactiva_DevuelveValidationFailed()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);
        var universities = new FakeUniversityRepository();
        var university = University.Restore(Guid.NewGuid(), "Universidad X", false, Now);
        universities.Items.Add(university);
        var service = new ChangeMyUniversityService(accounts, universities);

        var result = await service.ChangeAsync(account.Id, university.Id);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task ChangeAsync_CuentaInexistente_DevuelveNotFound()
    {
        var service = new ChangeMyUniversityService(new InMemoryAccountRepository(), new FakeUniversityRepository());

        var result = await service.ChangeAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ChangeAsync_CuentaSuspendida_DevuelveNotFound()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu", status: AccountStatus.Suspended);
        await accounts.AddAsync(account);
        var universities = new FakeUniversityRepository();
        var university = University.Restore(Guid.NewGuid(), "Universidad X", true, Now);
        universities.Items.Add(university);
        var service = new ChangeMyUniversityService(accounts, universities);

        var result = await service.ChangeAsync(account.Id, university.Id);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }
}
