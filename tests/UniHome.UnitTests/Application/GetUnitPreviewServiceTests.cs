using UniHome.Application.Common;
using UniHome.Application.Units.Preview;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Universities;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class GetUnitPreviewServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    [Fact]
    public async Task Preview_DebeConstruirDesdeDatosPersistidosSinDeclararPublicada()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        PublicationServiceTestData.Eligibility(accounts, account);
        var universityId = Guid.NewGuid();
        var property = PublicationServiceTestData.CreateProperty(account.Id, universityId);
        var unit = PublicationServiceTestData.UnitFor(property.Id, account.Id, universityId);
        unit.AppendPhotos(
        [
            new UnitPhoto("foto-1", "image/png", 10, "hash", 0, true, Now)
        ]);

        var properties = new FakePropertyRepository();
        properties.Items.Add(property);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var universities = new FakeUniversityRepository
        {
            Location = new GeoCoordinate(19.4326, -99.1332)
        };
        universities.Items.Add(University.Restore(universityId, "Universidad", true, Now));
        var service = new GetUnitPreviewService(
            units,
            properties,
            universities,
            new FakeUnitPhotoStorage());

        var result = await service.GetAsync(account.Id, unit.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(PublicationState.Borrador, result.Value!.EstadoPublicacion);
        Assert.NotNull(result.Value.FotoPrincipalUrl);
        Assert.Equal(0, result.Value.DistanciaKm);
        Assert.Null(result.Value.TiempoTraslado);
        Assert.False(result.Value.ArrendadorVerificado);
    }

    [Fact]
    public async Task Preview_UnidadAjena_DebeDevolverForbidden()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        PublicationServiceTestData.Eligibility(accounts, account);
        var property = PublicationServiceTestData.CreateProperty(account.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, account.Id, property.UniversityId);
        var properties = new FakePropertyRepository();
        properties.Items.Add(property);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var service = new GetUnitPreviewService(
            units,
            properties,
            new FakeUniversityRepository(),
            new FakeUnitPhotoStorage());

        var result = await service.GetAsync(Guid.NewGuid(), unit.Id);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }
}
