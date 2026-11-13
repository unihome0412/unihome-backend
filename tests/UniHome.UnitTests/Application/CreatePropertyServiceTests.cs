using UniHome.Application.Common;
using UniHome.Application.Properties.CreateProperty;
using UniHome.Application.Properties.Exceptions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Universities;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class CreatePropertyServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    [Fact]
    public async Task Create_ConCoordenadas_DebeCrearBorrador()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var universityId = Guid.NewGuid();
        var universities = new FakeUniversityRepository();
        universities.Items.Add(University.Restore(
            universityId,
            "Universidad",
            true,
            Now));
        var properties = new FakePropertyRepository();
        var service = new CreatePropertyService(
            properties,
            universities,
            eligibility,
            new FixedClock(Now));

        var result = await service.CreateAsync(account.Id, new CreatePropertyCommand(
            "Casa Centro",
            "Av. Juárez 100",
            "Centro",
            PropertyType.Casa,
            universityId,
            19.4326,
            -99.1332,
            null,
            null));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.False(result.Value!.LocationSuggested);
        Assert.Single(properties.Items);
        Assert.Equal(PublicationState.Borrador, result.Value.Property.PublicationState);
    }

    [Fact]
    public async Task Create_SinCoordenadas_UsaUbicacionSugerida()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var universityId = Guid.NewGuid();
        var universities = new FakeUniversityRepository
        {
            Location = new GeoCoordinate(19.43, -99.13)
        };
        universities.Items.Add(University.Restore(universityId, "Universidad", true, Now));
        var service = new CreatePropertyService(
            new FakePropertyRepository(),
            universities,
            eligibility,
            new FixedClock(Now));

        var result = await service.CreateAsync(account.Id, new CreatePropertyCommand(
            "Casa Centro",
            "Av. Juárez 100",
            "Centro",
            PropertyType.Casa,
            universityId,
            null,
            null,
            null,
            null));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.True(result.Value!.LocationSuggested);
        Assert.Equal(19.43, result.Value.Property.Latitude);
    }

    [Fact]
    public async Task Create_SinUbicacionSugerida_DebeDevolverValidacion()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var universityId = Guid.NewGuid();
        var universities = new FakeUniversityRepository();
        universities.Items.Add(University.Restore(universityId, "Universidad", true, Now));
        var service = new CreatePropertyService(
            new FakePropertyRepository(),
            universities,
            eligibility,
            new FixedClock(Now));

        var result = await service.CreateAsync(account.Id, new CreatePropertyCommand(
            "Casa Centro",
            "Av. Juárez 100",
            "Centro",
            PropertyType.Casa,
            universityId,
            null,
            null,
            null,
            null));

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.Contains("latitud", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Create_MenorDeEdad_DebeDevolverForbidden()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Minor();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var service = new CreatePropertyService(
            new FakePropertyRepository(),
            new FakeUniversityRepository(),
            eligibility,
            new FixedClock(Now));

        var result = await service.CreateAsync(account.Id, new CreatePropertyCommand(
            "Casa",
            "Calle 1",
            "Centro",
            PropertyType.Casa,
            Guid.NewGuid(),
            19,
            -99,
            null,
            null));

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task Create_Duplicado_DebeDevolverConflict()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var universityId = Guid.NewGuid();
        var universities = new FakeUniversityRepository();
        universities.Items.Add(University.Restore(universityId, "Universidad", true, Now));
        var service = new CreatePropertyService(
            new FakePropertyRepository { AddException = new DuplicatePropertyException() },
            universities,
            eligibility,
            new FixedClock(Now));

        var result = await service.CreateAsync(account.Id, new CreatePropertyCommand(
            "Casa",
            "Calle 1",
            "Centro",
            PropertyType.Casa,
            universityId,
            19,
            -99,
            null,
            null));

        Assert.Equal(OperationStatus.Conflict, result.Status);
    }
}
