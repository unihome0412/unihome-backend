using UniHome.Application.Universities.SearchUniversities;
using UniHome.Domain.Universities;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class SearchUniversitiesServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Search_SinTermino_DebeDevolverListaVacia(string? search)
    {
        var repository = new FakeUniversityRepository();
        var service = new SearchUniversitiesService(repository);

        var result = await service.SearchAsync(search);

        Assert.Empty(result.Universities);
        Assert.Empty(result.ValidationErrors);
        Assert.Null(repository.LastSearch);
    }

    [Fact]
    public async Task Search_DebeNormalizarYLimitarA10Resultados()
    {
        var repository = new FakeUniversityRepository();

        for (var index = 0; index < 15; index++)
        {
            repository.Items.Add(University.Restore(
                Guid.NewGuid(),
                $"Universidad Tecnológica {index:D2}",
                isActive: true,
                Now));
        }

        repository.Items.Add(University.Restore(
            Guid.NewGuid(),
            "Universidad Tecnológica Inactiva",
            isActive: false,
            Now));

        var service = new SearchUniversitiesService(repository);

        var result = await service.SearchAsync("  TECNOLÓGICA  ");

        Assert.Equal("tecnologica", repository.LastSearch);
        Assert.Equal(10, repository.LastLimit);
        Assert.Equal(10, result.Universities.Count);
        Assert.DoesNotContain(
            result.Universities,
            university => !university.IsActive);
    }

    [Fact]
    public async Task Search_ConTerminoExcesivo_DebeDevolverErrorDeValidacion()
    {
        var repository = new FakeUniversityRepository();
        var service = new SearchUniversitiesService(repository);

        var result = await service.SearchAsync(
            new string('a', SearchUniversitiesService.MaxSearchLength + 1));

        Assert.Empty(result.Universities);
        Assert.Contains("buscar", result.ValidationErrors.Keys);
        Assert.Null(repository.LastSearch);
    }
}
