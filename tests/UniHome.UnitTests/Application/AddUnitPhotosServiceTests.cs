using UniHome.Application.Common;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Profiles;
using UniHome.Application.Units.Photos;
using UniHome.Domain.Accounts;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class AddUnitPhotosServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    [Fact]
    public async Task Add_PngValido_DebeGuardarYMarcarPrimeraComoPrincipal()
    {
        var context = CreateContext();
        var service = new AddUnitPhotosService(
            context.Units,
            context.Storage,
            context.Eligibility,
            new FixedClock(Now));

        var result = await service.AddAsync(
            context.Account.Id,
            context.Unit.Id,
            [new UnitPhotoUpload("foto.png", "image/png", PublicationServiceTestData.PngBytes())]);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Single(result.Value!.Photos);
        Assert.True(result.Value.Photos[0].IsPrimary);
        Assert.Single(context.Storage.Saved);
    }

    [Fact]
    public async Task Add_ContenidoFalso_DebeDevolverValidacion()
    {
        var context = CreateContext();
        var service = new AddUnitPhotosService(
            context.Units,
            context.Storage,
            context.Eligibility,
            new FixedClock(Now));

        var result = await service.AddAsync(
            context.Account.Id,
            context.Unit.Id,
            [new UnitPhotoUpload("foto.jpg", "image/jpeg", [0x00, 0x01, 0x02])]);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.Empty(context.Storage.Saved);
    }

    [Fact]
    public async Task Add_MasDeDiez_DebeDevolverValidacion()
    {
        var context = CreateContext();
        var service = new AddUnitPhotosService(
            context.Units,
            context.Storage,
            context.Eligibility,
            new FixedClock(Now));
        var uploads = Enumerable.Range(0, Unit.MaxPhotos + 1)
            .Select(index => new UnitPhotoUpload(
                $"foto{index}.png",
                "image/png",
                PublicationServiceTestData.PngBytes()))
            .ToArray();

        var result = await service.AddAsync(
            context.Account.Id,
            context.Unit.Id,
            uploads);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.Empty(context.Storage.Saved);
    }

    [Fact]
    public async Task Add_CuandoAlmacenamientoFalla_DebePropagar503YLimpiar()
    {
        var context = CreateContext();
        context.Storage.ThrowOnSave = true;
        var service = new AddUnitPhotosService(
            context.Units,
            context.Storage,
            context.Eligibility,
            new FixedClock(Now));

        await Assert.ThrowsAsync<StorageUnavailableException>(() =>
            service.AddAsync(
                context.Account.Id,
                context.Unit.Id,
                [new UnitPhotoUpload("foto.png", "image/png", PublicationServiceTestData.PngBytes())]));

        Assert.Empty(context.Storage.Saved);
    }

    [Fact]
    public async Task Add_UnidadAjena_DebeDevolverForbidden()
    {
        var context = CreateContext();
        var service = new AddUnitPhotosService(
            context.Units,
            context.Storage,
            context.Eligibility,
            new FixedClock(Now));

        var result = await service.AddAsync(
            Guid.NewGuid(),
            context.Unit.Id,
            [new UnitPhotoUpload("foto.png", "image/png", PublicationServiceTestData.PngBytes())]);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    private static Context CreateContext()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var property = PublicationServiceTestData.CreateProperty(account.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, account.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        return new Context(account, unit, units, eligibility, new FakeUnitPhotoStorage());
    }

    private sealed record Context(
        Account Account,
        Unit Unit,
        FakeUnitRepository Units,
        PublishingEligibilityService Eligibility,
        FakeUnitPhotoStorage Storage);
}
