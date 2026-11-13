using UniHome.Application.Universities.UpdateUniversity;
using UniHome.Domain.Universities;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class UpdateUniversityServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Update_ConDatosValidos_DebeActualizarNombreNormalizado()
    {
        var repository = new FakeUniversityRepository();
        var university = University.Restore(
            Guid.NewGuid(),
            "Universidad del Norte",
            true,
            Now);
        repository.Items.Add(university);
        var service = new UpdateUniversityService(repository);

        var result = await service.UpdateAsync(
            university.Id,
            "Universidad Autónoma del Norte");

        Assert.Equal(UpdateUniversityStatus.Updated, result.Status);
        Assert.Equal("Universidad Autónoma del Norte", result.University!.NameAndCampus);
        Assert.Equal(
            "universidad autonoma del norte",
            result.University.NormalizedName);
    }

    [Fact]
    public async Task Update_Inexistente_DebeDevolverNotFound()
    {
        var repository = new FakeUniversityRepository();
        var service = new UpdateUniversityService(repository);

        var result = await service.UpdateAsync(Guid.NewGuid(), "Universidad");

        Assert.Equal(UpdateUniversityStatus.NotFound, result.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Update_ConNombreVacio_DebeDevolverValidacion(string? value)
    {
        var repository = new FakeUniversityRepository();
        var university = University.Restore(Guid.NewGuid(), "Universidad", true, Now);
        repository.Items.Add(university);
        var service = new UpdateUniversityService(repository);

        var result = await service.UpdateAsync(university.Id, value);

        Assert.Equal(UpdateUniversityStatus.ValidationFailed, result.Status);
        Assert.Contains("nombreYCampus", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Update_DuplicadoNormalizado_DebeDevolverConflicto()
    {
        var repository = new FakeUniversityRepository
        {
            ThrowDuplicateOnUpdate = true
        };
        var university = University.Restore(Guid.NewGuid(), "Universidad", true, Now);
        repository.Items.Add(university);
        var service = new UpdateUniversityService(repository);

        var result = await service.UpdateAsync(university.Id, "Universidad Duplicada");

        Assert.Equal(UpdateUniversityStatus.Duplicate, result.Status);
    }

    [Fact]
    public async Task Update_EdicionConcurrentePerdedora_DebeDevolverConflicto()
    {
        var repository = new FakeUniversityRepository { UpdateResult = false };
        var university = University.Restore(Guid.NewGuid(), "Universidad", true, Now);
        repository.Items.Add(university);
        var service = new UpdateUniversityService(repository);

        var result = await service.UpdateAsync(university.Id, "Universidad Nueva");

        Assert.Equal(UpdateUniversityStatus.Conflict, result.Status);
    }
}
