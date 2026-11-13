using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Universities.CreateUniversity;
using UniHome.Domain.Universities;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class CreateUniversityServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_ConDatosValidos_DebePersistirYDevolverUniversidadActiva()
    {
        var repository = new FakeUniversityRepository();
        var service = new CreateUniversityService(repository, new FixedClock(Now));

        var result = await service.CreateAsync(
            new CreateUniversityCommand("Universidad Tecnológica de Monterrey"));

        Assert.Equal(CreateUniversityStatus.Created, result.Status);
        Assert.NotNull(result.University);
        Assert.True(result.University.IsActive);
        Assert.Equal(Now, result.University.CreatedAt);
        Assert.Single(repository.Items);
        Assert.Equal("universidad tecnologica de monterrey", repository.Items[0].NormalizedName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_SinNombreYCampus_DebeDevolverValidacionSinPersistir(
        string? value)
    {
        var repository = new FakeUniversityRepository();
        var service = new CreateUniversityService(repository, new FixedClock(Now));

        var result = await service.CreateAsync(new CreateUniversityCommand(value));

        Assert.Equal(CreateUniversityStatus.ValidationFailed, result.Status);
        Assert.Contains("nombreYCampus", result.ValidationErrors.Keys);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Create_ConLongitudExcesiva_DebeDevolverValidacion()
    {
        var repository = new FakeUniversityRepository();
        var service = new CreateUniversityService(repository, new FixedClock(Now));

        var result = await service.CreateAsync(
            new CreateUniversityCommand(new string('a', University.MaxNameAndCampusLength + 1)));

        Assert.Equal(CreateUniversityStatus.ValidationFailed, result.Status);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Create_ConDuplicadoNormalizado_DebeDevolverConflictoSinFiltrarDetalles()
    {
        var repository = new FakeUniversityRepository { ThrowDuplicate = true };
        var service = new CreateUniversityService(repository, new FixedClock(Now));

        var result = await service.CreateAsync(
            new CreateUniversityCommand("Universidad Tecnológica de Monterrey"));

        Assert.Equal(CreateUniversityStatus.Duplicate, result.Status);
        Assert.Null(result.University);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    public async Task Create_CuandoElAlmacenamientoFalla_DebePropagarErrorDeServicio()
    {
        var repository = new FakeUniversityRepository
        {
            AddException = new StorageUnavailableException()
        };
        var service = new CreateUniversityService(repository, new FixedClock(Now));

        await Assert.ThrowsAsync<StorageUnavailableException>(() =>
            service.CreateAsync(new CreateUniversityCommand("Universidad del Norte")));
    }
}
