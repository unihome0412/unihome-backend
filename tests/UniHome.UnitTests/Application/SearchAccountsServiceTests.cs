using UniHome.Application.Common;
using UniHome.Application.Identity.Admin;
using UniHome.Application.Profiles.GetProfile;
using UniHome.Domain.Accounts;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class SearchAccountsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SearchAsync_SinFiltro_DevuelveTodasLasCoincidencias()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);
        var service = new SearchAccountsService(accounts, new FakeUnitRepository());

        var result = await service.SearchAsync(null, AccountSearchFilter.Todos, page: 1, pageSize: 20);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(1, result.Value!.Total);
        Assert.Equal(AccountLabel.UsuarioUniHome, Assert.Single(result.Value.Items).Label);
    }

    [Fact]
    public async Task SearchAsync_FiltradoPorConPublicaciones_ExcluyeCuentasSinUnidadAprobada()
    {
        var accounts = new InMemoryAccountRepository();
        var sinPublicaciones = AccountTestData.Create("sin@uni.edu");
        var conPublicaciones = AccountTestData.Create("con@uni.edu");
        await accounts.AddAsync(sinPublicaciones);
        await accounts.AddAsync(conPublicaciones);
        var units = new FakeUnitRepository();
        units.Items.Add(CreateUnit(conPublicaciones.Id, PublicationState.Aprobada));
        var service = new SearchAccountsService(accounts, units);

        var result = await service.SearchAsync(null, AccountSearchFilter.ConPublicaciones, page: 1, pageSize: 20);

        Assert.Equal(OperationStatus.Success, result.Status);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(conPublicaciones.Id, item.Account.Id);
    }

    [Fact]
    public async Task SearchAsync_FiltradoPorEnRevision_SoloIncluyeCuentasConUnidadEnRevisionSinAprobadas()
    {
        var accounts = new InMemoryAccountRepository();
        var enRevision = AccountTestData.Create("revision@uni.edu");
        await accounts.AddAsync(enRevision);
        var units = new FakeUnitRepository();
        units.Items.Add(CreateUnit(enRevision.Id, PublicationState.EnRevision));
        var service = new SearchAccountsService(accounts, units);

        var result = await service.SearchAsync(null, AccountSearchFilter.EnRevision, page: 1, pageSize: 20);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Single(result.Value!.Items);
    }

    [Fact]
    public async Task SearchAsync_PorTexto_SoloCoincideConNombreOCorreo()
    {
        var accounts = new InMemoryAccountRepository();
        var match = AccountTestData.Create("buscar.especial@uni.edu");
        var other = AccountTestData.Create("otra@uni.edu");
        await accounts.AddAsync(match);
        await accounts.AddAsync(other);
        var service = new SearchAccountsService(accounts, new FakeUnitRepository());

        var result = await service.SearchAsync("especial", AccountSearchFilter.Todos, page: 1, pageSize: 20);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(match.Id, Assert.Single(result.Value!.Items).Account.Id);
    }

    [Fact]
    public async Task SearchAsync_Pagina_RespetaPageSize()
    {
        var accounts = new InMemoryAccountRepository();
        await accounts.AddAsync(AccountTestData.Create("uno@uni.edu"));
        await accounts.AddAsync(AccountTestData.Create("dos@uni.edu"));
        var service = new SearchAccountsService(accounts, new FakeUnitRepository());

        var result = await service.SearchAsync(null, AccountSearchFilter.Todos, page: 1, pageSize: 1);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(2, result.Value!.Total);
        Assert.Single(result.Value.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100_001)]
    public async Task SearchAsync_ConPaginaInvalida_DevuelveValidationFailed(int page)
    {
        var service = new SearchAccountsService(new InMemoryAccountRepository(), new FakeUnitRepository());

        var result = await service.SearchAsync(null, AccountSearchFilter.Todos, page, pageSize: 20);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task SearchAsync_ConTamanoDePaginaInvalido_DevuelveValidationFailed(int pageSize)
    {
        var service = new SearchAccountsService(new InMemoryAccountRepository(), new FakeUnitRepository());

        var result = await service.SearchAsync(null, AccountSearchFilter.Todos, page: 1, pageSize);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    private static Unit CreateUnit(Guid ownerId, PublicationState state) =>
        Unit.Restore(
            Guid.NewGuid(), Guid.NewGuid(), ownerId, Guid.NewGuid(), UnitType.Habitacion,
            Occupancy.Individual, 1, 1500m, 12m, ForWhom.Mixto, BathroomType.Compartido,
            FurnitureType.SinMuebles, furnitureItems: null, template: null, zoneInfo: null,
            rules: null, services: null, photos: null, state, Now);
}
