using UniHome.Application.Common;
using UniHome.Application.Reparaciones;
using UniHome.Application.Units.Photos;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class CreateReparacionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    [Fact]
    public async Task CreateAsync_ComoPropietarioSinFoto_DebeCrearReporteEnPendiente()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out var ownerId);
        units.Items.Add(unit);
        var reparaciones = new FakeReparacionRepository();
        var service = new CreateReparacionService(
            units, new FakeOccupancyLinkRepository(), reparaciones, new FakeUnitPhotoStorage(), new FixedClock(Now));

        var result = await service.CreateAsync(ownerId, unit.Id, "Fuga", "Hay una fuga", null);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("Fuga", result.Value!.Title);
        Assert.Null(result.Value.Photo);
        Assert.Single(reparaciones.Items);
    }

    [Fact]
    public async Task CreateAsync_ComoInquilinoActivoConFoto_DebeAlmacenarLaFoto()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out _);
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var tenantId = Guid.NewGuid();
        links.Items.Add(OccupancyLink.CreateActive(tenantId, unit.Id, Now));
        var storage = new FakeUnitPhotoStorage();
        var reparaciones = new FakeReparacionRepository();
        var service = new CreateReparacionService(units, links, reparaciones, storage, new FixedClock(Now));

        var result = await service.CreateAsync(
            tenantId, unit.Id, "Foco", "Fundido", new UnitPhotoUpload("foto.png", "image/png", PngBytes));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotNull(result.Value!.Photo);
        Assert.Single(storage.Saved);
    }

    [Fact]
    public async Task CreateAsync_UnidadInexistente_DevuelveNotFound()
    {
        var service = new CreateReparacionService(
            new FakeUnitRepository(),
            new FakeOccupancyLinkRepository(),
            new FakeReparacionRepository(),
            new FakeUnitPhotoStorage(),
            new FixedClock(Now));

        var result = await service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), "Título", "Descripción", null);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task CreateAsync_CuentaSinAccesoALaUnidad_DevuelveForbidden()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out _);
        units.Items.Add(unit);
        var service = new CreateReparacionService(
            units,
            new FakeOccupancyLinkRepository(),
            new FakeReparacionRepository(),
            new FakeUnitPhotoStorage(),
            new FixedClock(Now));

        var result = await service.CreateAsync(Guid.NewGuid(), unit.Id, "Título", "Descripción", null);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ConTituloVacio_DevuelveValidationFailed()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out var ownerId);
        units.Items.Add(unit);
        var reparaciones = new FakeReparacionRepository();
        var service = new CreateReparacionService(
            units, new FakeOccupancyLinkRepository(), reparaciones, new FakeUnitPhotoStorage(), new FixedClock(Now));

        var result = await service.CreateAsync(ownerId, unit.Id, string.Empty, "Descripción", null);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors.ContainsKey("titulo"));
        Assert.Empty(reparaciones.Items);
    }

    [Fact]
    public async Task CreateAsync_ConFotoInvalida_DevuelveValidationFailedSinGuardarNiAlmacenar()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out var ownerId);
        units.Items.Add(unit);
        var reparaciones = new FakeReparacionRepository();
        var storage = new FakeUnitPhotoStorage();
        var service = new CreateReparacionService(
            units, new FakeOccupancyLinkRepository(), reparaciones, storage, new FixedClock(Now));
        var invalidPhoto = new UnitPhotoUpload("foto.txt", "text/plain", [1, 2, 3]);

        var result = await service.CreateAsync(ownerId, unit.Id, "Título", "Descripción", invalidPhoto);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors.ContainsKey("foto"));
        Assert.Empty(reparaciones.Items);
        Assert.Empty(storage.Saved);
    }

    private static Unit CreateUnit(out Guid ownerId)
    {
        ownerId = Guid.NewGuid();
        return Unit.Create(
            Guid.NewGuid(), ownerId, Guid.NewGuid(), UnitType.Habitacion, Occupancy.Compartida,
            3, 1500m, 12m, ForWhom.Mixto, BathroomType.Compartido, FurnitureType.SinMuebles,
            furnitureItems: null, template: null, Now);
    }
}
