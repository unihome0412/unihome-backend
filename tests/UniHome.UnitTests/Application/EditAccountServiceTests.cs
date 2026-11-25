using UniHome.Application.Common;
using UniHome.Application.Identity.Admin;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class EditAccountServiceTests
{
    [Fact]
    public async Task EditAsync_ConDatosValidos_ActualizaNombreYCorreo()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);
        var service = new EditAccountService(accounts);

        var result = await service.EditAsync(account.Id, "Nuevo Nombre", "nuevo@uni.edu");

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("Nuevo Nombre", result.Value!.FullName);
        Assert.Equal("nuevo@uni.edu", result.Value.Email.Value);
    }

    [Fact]
    public async Task EditAsync_CuentaInexistente_DevuelveNotFound()
    {
        var service = new EditAccountService(new InMemoryAccountRepository());

        var result = await service.EditAsync(Guid.NewGuid(), "Nombre", "correo@uni.edu");

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task EditAsync_ConCorreoInvalido_DevuelveValidationFailed()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);
        var service = new EditAccountService(accounts);

        var result = await service.EditAsync(account.Id, "Nombre", "no-es-un-correo");

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors.ContainsKey("correo"));
    }

    [Fact]
    public async Task EditAsync_ConNombreInvalido_DevuelveValidationFailed()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);
        var service = new EditAccountService(accounts);

        var result = await service.EditAsync(account.Id, " ", "otro@uni.edu");

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors.ContainsKey("nombreCompleto"));
    }

    [Fact]
    public async Task EditAsync_ConCorreoYaUsadoPorOtraCuenta_DevuelveValidationFailed()
    {
        var accounts = new InMemoryAccountRepository();
        var existing = AccountTestData.Create("existente@uni.edu");
        await accounts.AddAsync(existing);
        var target = AccountTestData.Create("objetivo@uni.edu");
        await accounts.AddAsync(target);
        var service = new EditAccountService(accounts);

        var result = await service.EditAsync(target.Id, "Nombre", "existente@uni.edu");

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors.ContainsKey("correo"));
    }

    [Fact]
    public async Task EditAsync_ConElMismoCorreoPropio_DebePermitirlo()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);
        var service = new EditAccountService(accounts);

        var result = await service.EditAsync(account.Id, "Nuevo Nombre", "ana@uni.edu");

        Assert.Equal(OperationStatus.Success, result.Status);
    }
}
