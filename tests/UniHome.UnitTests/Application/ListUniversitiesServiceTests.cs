using UniHome.Application.Universities.ListUniversities;
using UniHome.Domain.Universities;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ListUniversitiesServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_DebeIncluirActivasEInactivasConTotal()
    {
        var repository = new FakeUniversityRepository();
        repository.Items.Add(University.Restore(Guid.NewGuid(), "Universidad A", true, Now));
        repository.Items.Add(University.Restore(Guid.NewGuid(), "Universidad B", false, Now));
        repository.Items.Add(University.Restore(Guid.NewGuid(), "Universidad C", true, Now));
        var service = new ListUniversitiesService(repository);

        var result = await service.ListAsync(2, 2);

        Assert.NotNull(result.Page);
        Assert.Equal(3, result.Page.Total);
        Assert.Single(result.Page.Items);
        Assert.Equal("Universidad C", result.Page.Items[0].NameAndCampus);
        Assert.Equal(2, repository.LastPage);
        Assert.Equal(2, repository.LastPageSize);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task List_ConPaginacionInvalida_DebeDevolverValidacion(
        int page,
        int pageSize)
    {
        var repository = new FakeUniversityRepository();
        var service = new ListUniversitiesService(repository);

        var result = await service.ListAsync(page, pageSize);

        Assert.Null(result.Page);
        Assert.NotEmpty(result.ValidationErrors);
    }
}
