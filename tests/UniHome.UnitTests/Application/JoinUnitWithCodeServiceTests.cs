using UniHome.Application.OccupancyLinks;
using UniHome.Application.OccupancyLinks.JoinWithCode;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class JoinUnitWithCodeServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    [Fact]
    public async Task JoinAsync_ConCodigoVigente_VinculaYMarcaOcupadaSiSeLlenan()
    {
        var owner = PublicationServiceTestData.Adult("owner@uni.edu");
        var tenant = Guid.NewGuid();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var invitations = new FakeUnitInvitationRepository();
        invitations.Items.Add(UnitInvitation.Create(unit.Id, "ABCD2345", Now));
        var recalculator = new UnitAvailabilityRecalculator(units, links);
        var service = new JoinUnitWithCodeService(
            invitations, units, links, recalculator, new FixedClock(Now));

        var result = await service.JoinAsync(tenant, "abcd2345");

        Assert.Equal(JoinUnitWithCodeStatus.Linked, result.Status);
        Assert.Equal(unit.Id, result.Value!.UnidadId);
        Assert.Equal(OccupancyStatus.Activo, result.Value.Estado);
        Assert.Equal(UnitAvailability.Ocupado, unit.Availability);
    }

    [Fact]
    public async Task JoinAsync_UnidadCompartidaConCupo_PermiteVariosInquilinos()
    {
        var owner = PublicationServiceTestData.Adult("owner2@uni.edu");
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = Unit.Create(
            property.Id, owner.Id, property.UniversityId,
            UnitType.Habitacion, Occupancy.Compartida, 2, 2500, 18,
            ForWhom.Mixto, BathroomType.Compartido, FurnitureType.Amueblado, ["Cama"], null, Now);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var invitations = new FakeUnitInvitationRepository();
        invitations.Items.Add(UnitInvitation.Create(unit.Id, "ABCD2345", Now));
        var recalculator = new UnitAvailabilityRecalculator(units, links);
        var service = new JoinUnitWithCodeService(
            invitations, units, links, recalculator, new FixedClock(Now));

        var first = await service.JoinAsync(Guid.NewGuid(), "ABCD2345");
        Assert.Equal(UnitAvailability.Disponible, unit.Availability);

        var second = await service.JoinAsync(Guid.NewGuid(), "ABCD2345");

        Assert.Equal(JoinUnitWithCodeStatus.Linked, first.Status);
        Assert.Equal(JoinUnitWithCodeStatus.Linked, second.Status);
        Assert.Equal(UnitAvailability.Ocupado, unit.Availability);
    }

    [Fact]
    public async Task JoinAsync_UnidadLlena_DebeDevolverUnitFull()
    {
        var owner = PublicationServiceTestData.Adult("owner3@uni.edu");
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        links.Items.Add(OccupancyLink.CreateActive(Guid.NewGuid(), unit.Id, Now));
        var invitations = new FakeUnitInvitationRepository();
        invitations.Items.Add(UnitInvitation.Create(unit.Id, "ABCD2345", Now));
        var recalculator = new UnitAvailabilityRecalculator(units, links);
        var service = new JoinUnitWithCodeService(
            invitations, units, links, recalculator, new FixedClock(Now));

        var result = await service.JoinAsync(Guid.NewGuid(), "ABCD2345");

        Assert.Equal(JoinUnitWithCodeStatus.UnitFull, result.Status);
    }

    [Fact]
    public async Task JoinAsync_CodigoInexistente_DebeDevolverInvalidCode()
    {
        var service = new JoinUnitWithCodeService(
            new FakeUnitInvitationRepository(),
            new FakeUnitRepository(),
            new FakeOccupancyLinkRepository(),
            new UnitAvailabilityRecalculator(new FakeUnitRepository(), new FakeOccupancyLinkRepository()),
            new FixedClock(Now));

        var result = await service.JoinAsync(Guid.NewGuid(), "NOEXISTE");

        Assert.Equal(JoinUnitWithCodeStatus.InvalidCode, result.Status);
    }

    [Fact]
    public async Task JoinAsync_CodigoVacio_DebeDevolverValidationFailed()
    {
        var service = new JoinUnitWithCodeService(
            new FakeUnitInvitationRepository(),
            new FakeUnitRepository(),
            new FakeOccupancyLinkRepository(),
            new UnitAvailabilityRecalculator(new FakeUnitRepository(), new FakeOccupancyLinkRepository()),
            new FixedClock(Now));

        var result = await service.JoinAsync(Guid.NewGuid(), "   ");

        Assert.Equal(JoinUnitWithCodeStatus.ValidationFailed, result.Status);
        Assert.Contains("codigo", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task JoinAsync_CodigoVencido_DebeDevolverExpiredCode()
    {
        var owner = PublicationServiceTestData.Adult("owner4@uni.edu");
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var invitations = new FakeUnitInvitationRepository();
        invitations.Items.Add(UnitInvitation.Create(unit.Id, "ABCD2345", Now));
        var links = new FakeOccupancyLinkRepository();
        var recalculator = new UnitAvailabilityRecalculator(units, links);
        var service = new JoinUnitWithCodeService(
            invitations, units, links, recalculator, new FixedClock(Now.AddDays(8)));

        var result = await service.JoinAsync(Guid.NewGuid(), "ABCD2345");

        Assert.Equal(JoinUnitWithCodeStatus.ExpiredCode, result.Status);
    }

    [Fact]
    public async Task JoinAsync_PropietarioIntentaUnirseAsuPropiaUnidad_DebeDevolverSelfJoinForbidden()
    {
        var owner = PublicationServiceTestData.Adult("owner5@uni.edu");
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var invitations = new FakeUnitInvitationRepository();
        invitations.Items.Add(UnitInvitation.Create(unit.Id, "ABCD2345", Now));
        var links = new FakeOccupancyLinkRepository();
        var recalculator = new UnitAvailabilityRecalculator(units, links);
        var service = new JoinUnitWithCodeService(
            invitations, units, links, recalculator, new FixedClock(Now));

        var result = await service.JoinAsync(owner.Id, "ABCD2345");

        Assert.Equal(JoinUnitWithCodeStatus.SelfJoinForbidden, result.Status);
    }

    [Fact]
    public async Task JoinAsync_InquilinoYaActivo_EsIdempotenteYNoDuplicaConteo()
    {
        var owner = PublicationServiceTestData.Adult("owner6@uni.edu");
        var tenant = Guid.NewGuid();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var invitations = new FakeUnitInvitationRepository();
        invitations.Items.Add(UnitInvitation.Create(unit.Id, "ABCD2345", Now));
        var links = new FakeOccupancyLinkRepository();
        var recalculator = new UnitAvailabilityRecalculator(units, links);
        var service = new JoinUnitWithCodeService(
            invitations, units, links, recalculator, new FixedClock(Now));

        var first = await service.JoinAsync(tenant, "ABCD2345");
        var second = await service.JoinAsync(tenant, "ABCD2345");

        Assert.Equal(JoinUnitWithCodeStatus.Linked, first.Status);
        Assert.Equal(JoinUnitWithCodeStatus.Linked, second.Status);
        Assert.Single(links.Items);
    }

    [Fact]
    public async Task JoinAsync_InquilinoConVinculoFinalizado_Reactiva()
    {
        var owner = PublicationServiceTestData.Adult("owner7@uni.edu");
        var tenant = Guid.NewGuid();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var invitations = new FakeUnitInvitationRepository();
        invitations.Items.Add(UnitInvitation.Create(unit.Id, "ABCD2345", Now));
        var links = new FakeOccupancyLinkRepository();
        var endedLink = OccupancyLink.CreateActive(tenant, unit.Id, Now);
        endedLink.End(OccupancyEndReason.AbandonoInquilino, Now.AddDays(1));
        links.Items.Add(endedLink);
        var recalculator = new UnitAvailabilityRecalculator(units, links);
        var service = new JoinUnitWithCodeService(
            invitations, units, links, recalculator, new FixedClock(Now.AddDays(2)));

        var result = await service.JoinAsync(tenant, "ABCD2345");

        Assert.Equal(JoinUnitWithCodeStatus.Linked, result.Status);
        Assert.Equal(OccupancyStatus.Activo, result.Value!.Estado);
        Assert.Single(links.Items);
        Assert.Equal(UnitAvailability.Ocupado, unit.Availability);
    }
}
