using UniHome.Application.Rentals;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Rentals;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class RentPeriodResolverTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    private static IReadOnlyCollection<RentConcept> AllConcepts() =>
    [
        new RentConcept(RentConceptName.Renta, true),
        new RentConcept(RentConceptName.Mantenimiento, true),
        new RentConcept(RentConceptName.Luz, false),
        new RentConcept(RentConceptName.Gas, false)
    ];

    [Fact]
    public async Task ResolveAsync_PeriodoExistente_NoHeredaNiPersisteNada()
    {
        var links = new FakeOccupancyLinkRepository();
        var rentPeriods = new FakeRentPeriodRepository();
        var unitId = Guid.NewGuid();
        var existing = RentPeriod.Configure(unitId, "2026-11", 6000m, 5, AllConcepts(), Now);
        rentPeriods.Items.Add(existing);
        var resolver = new RentPeriodResolver(rentPeriods, links, new FixedClock(Now));

        var resolved = await resolver.ResolveAsync(unitId, "2026-11");

        Assert.Same(existing, resolved);
        Assert.Single(rentPeriods.Items);
    }

    [Fact]
    public async Task ResolveAsync_SinPeriodoNiAnterior_DevuelveNull()
    {
        var links = new FakeOccupancyLinkRepository();
        var rentPeriods = new FakeRentPeriodRepository();
        var resolver = new RentPeriodResolver(rentPeriods, links, new FixedClock(Now));

        var resolved = await resolver.ResolveAsync(Guid.NewGuid(), "2026-11");

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveAsync_HayAnteriorYMismosInquilinos_HeredaYPersiste()
    {
        var unitId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var links = new FakeOccupancyLinkRepository();
        links.Items.Add(OccupancyLink.CreateActive(tenantA, unitId, Now));
        links.Items.Add(OccupancyLink.CreateActive(tenantB, unitId, Now));
        var rentPeriods = new FakeRentPeriodRepository();
        var previous = RentPeriod.Configure(unitId, "2026-10", 4000m, 5, AllConcepts(), Now);
        previous.ApplyEqualSplit([tenantA, tenantB], Now);
        rentPeriods.Items.Add(previous);
        var resolver = new RentPeriodResolver(rentPeriods, links, new FixedClock(Now));

        var resolved = await resolver.ResolveAsync(unitId, "2026-11");

        Assert.NotNull(resolved);
        Assert.Equal("2026-11", resolved!.Periodo);
        Assert.Equal(RentSplitMethod.Igual, resolved.SplitMethod);
        Assert.Equal(2, rentPeriods.Items.Count);
        Assert.Contains(rentPeriods.Items, item => item.Periodo == "2026-11");
    }

    [Fact]
    public async Task ResolveAsync_HayAnteriorPeroInquilinosCambiaron_HeredaConDivisionPendiente()
    {
        var unitId = Guid.NewGuid();
        var links = new FakeOccupancyLinkRepository();
        links.Items.Add(OccupancyLink.CreateActive(Guid.NewGuid(), unitId, Now));
        var rentPeriods = new FakeRentPeriodRepository();
        var previous = RentPeriod.Configure(unitId, "2026-10", 4000m, 5, AllConcepts(), Now);
        previous.ApplyEqualSplit([Guid.NewGuid(), Guid.NewGuid()], Now);
        rentPeriods.Items.Add(previous);
        var resolver = new RentPeriodResolver(rentPeriods, links, new FixedClock(Now));

        var resolved = await resolver.ResolveAsync(unitId, "2026-11");

        Assert.NotNull(resolved);
        Assert.True(resolved!.IsSplitPending);
    }
}
