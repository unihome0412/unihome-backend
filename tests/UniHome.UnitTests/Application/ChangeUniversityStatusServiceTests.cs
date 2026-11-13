using UniHome.Application.Universities.ChangeUniversityStatus;
using UniHome.Domain.Universities;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ChangeUniversityStatusServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Change_Desactivar_DebeConservarDocumentoYCambiarEstado()
    {
        var repository = new FakeUniversityRepository();
        var university = University.Restore(Guid.NewGuid(), "Universidad", true, Now);
        repository.Items.Add(university);
        var service = new ChangeUniversityStatusService(repository);

        var result = await service.ChangeAsync(university.Id, isActive: false);

        Assert.Equal(ChangeUniversityStatus.Updated, result.Status);
        Assert.False(result.University!.IsActive);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task Change_Activar_DebeCambiarEstado()
    {
        var repository = new FakeUniversityRepository();
        var university = University.Restore(Guid.NewGuid(), "Universidad", false, Now);
        repository.Items.Add(university);
        var service = new ChangeUniversityStatusService(repository);

        var result = await service.ChangeAsync(university.Id, isActive: true);

        Assert.Equal(ChangeUniversityStatus.Updated, result.Status);
        Assert.True(result.University!.IsActive);
    }

    [Fact]
    public async Task Change_Inexistente_DebeDevolverNotFound()
    {
        var service = new ChangeUniversityStatusService(new FakeUniversityRepository());

        var result = await service.ChangeAsync(Guid.NewGuid(), isActive: false);

        Assert.Equal(ChangeUniversityStatus.NotFound, result.Status);
    }
}
