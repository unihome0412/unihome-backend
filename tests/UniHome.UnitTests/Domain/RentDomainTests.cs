using UniHome.Domain.Rentals;

namespace UniHome.UnitTests.Domain;

public sealed class RentDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    private static IReadOnlyCollection<RentConcept> AllConcepts() =>
    [
        new RentConcept(RentConceptName.Renta, true),
        new RentConcept(RentConceptName.Mantenimiento, true),
        new RentConcept(RentConceptName.Luz, false),
        new RentConcept(RentConceptName.Gas, false)
    ];

    [Fact]
    public void Configure_ConDatosValidos_QuedaConDivisionPendiente()
    {
        var unitId = Guid.NewGuid();

        var period = RentPeriod.Configure(unitId, "2026-11", 6000m, 5, AllConcepts(), Now);

        Assert.Equal(unitId, period.UnitId);
        Assert.Equal("2026-11", period.Periodo);
        Assert.Equal(6000m, period.TotalMxn);
        Assert.Equal(5, period.CutoffDay);
        Assert.Equal(4, period.Concepts.Count);
        Assert.True(period.IsSplitPending);
        Assert.Empty(period.Shares);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("26-11")]
    [InlineData("2026/11")]
    [InlineData("")]
    public void Configure_PeriodoInvalido_DebeRechazarse(string periodo)
    {
        Assert.Throws<ArgumentException>(() =>
            RentPeriod.Configure(Guid.NewGuid(), periodo, 6000m, 5, AllConcepts(), Now));
    }

    [Fact]
    public void Configure_UnidadVacia_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() =>
            RentPeriod.Configure(Guid.Empty, "2026-11", 6000m, 5, AllConcepts(), Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Configure_TotalNoPositivo_DebeRechazarse(decimal total)
    {
        Assert.Throws<ArgumentException>(() =>
            RentPeriod.Configure(Guid.NewGuid(), "2026-11", total, 5, AllConcepts(), Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Configure_DiaDeCorteFueraDeRango_DebeRechazarse(int cutoffDay)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RentPeriod.Configure(Guid.NewGuid(), "2026-11", 6000m, cutoffDay, AllConcepts(), Now));
    }

    [Fact]
    public void Configure_ConceptoFaltanteORepetido_DebeRechazarse()
    {
        var incompletos = new[] { new RentConcept(RentConceptName.Renta, true) };
        var repetidos = new[]
        {
            new RentConcept(RentConceptName.Renta, true),
            new RentConcept(RentConceptName.Renta, false),
            new RentConcept(RentConceptName.Luz, false),
            new RentConcept(RentConceptName.Gas, false)
        };

        Assert.Throws<ArgumentException>(() =>
            RentPeriod.Configure(Guid.NewGuid(), "2026-11", 6000m, 5, incompletos, Now));
        Assert.Throws<ArgumentException>(() =>
            RentPeriod.Configure(Guid.NewGuid(), "2026-11", 6000m, 5, repetidos, Now));
    }

    [Fact]
    public void SetConfiguration_SiempreReiniciaLaDivisionAPendiente()
    {
        var period = RentPeriod.Configure(Guid.NewGuid(), "2026-11", 6000m, 5, AllConcepts(), Now);
        period.ApplyEqualSplit([Guid.NewGuid(), Guid.NewGuid()], Now);
        Assert.False(period.IsSplitPending);

        period.SetConfiguration(9000m, 10, AllConcepts(), Now.AddMinutes(1));

        Assert.Equal(9000m, period.TotalMxn);
        Assert.Equal(10, period.CutoffDay);
        Assert.True(period.IsSplitPending);
        Assert.Empty(period.Shares);
    }

    [Fact]
    public void ApplyEqualSplit_MilEntreTresInquilinos_RepartaElCentavoSobranteYSumaExacto()
    {
        var period = RentPeriod.Configure(Guid.NewGuid(), "2026-11", 1000m, 5, AllConcepts(), Now);
        var tenants = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        period.ApplyEqualSplit(tenants, Now);

        Assert.Equal(RentSplitMethod.Igual, period.SplitMethod);
        Assert.Equal(3, period.Shares.Count);
        Assert.Equal(1000m, period.Shares.Sum(share => share.AmountMxn));
        // Ordenado por accountId ("N", ordinal): el primero absorbe el centavo sobrante.
        var ordered = tenants.OrderBy(id => id.ToString("N"), StringComparer.Ordinal).ToArray();
        Assert.Equal(333.34m, period.FindShare(ordered[0])!.AmountMxn);
        Assert.Equal(333.33m, period.FindShare(ordered[1])!.AmountMxn);
        Assert.Equal(333.33m, period.FindShare(ordered[2])!.AmountMxn);
    }

    [Fact]
    public void ApplyEqualSplit_DivisibleExacto_RepartePorIgual()
    {
        var period = RentPeriod.Configure(Guid.NewGuid(), "2026-11", 4000m, 5, AllConcepts(), Now);
        var tenants = new[] { Guid.NewGuid(), Guid.NewGuid() };

        period.ApplyEqualSplit(tenants, Now);

        Assert.All(period.Shares, share => Assert.Equal(2000m, share.AmountMxn));
    }

    [Fact]
    public void ApplyEqualSplit_SinInquilinosORepetido_DebeRechazarse()
    {
        var period = RentPeriod.Configure(Guid.NewGuid(), "2026-11", 1000m, 5, AllConcepts(), Now);
        var repeated = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => period.ApplyEqualSplit([], Now));
        Assert.Throws<ArgumentException>(() => period.ApplyEqualSplit([repeated, repeated], Now));
    }

    [Fact]
    public void ApplyCustomSplit_SumaDistintaDeLaRentaTotal_DebeRechazarse()
    {
        var period = RentPeriod.Configure(Guid.NewGuid(), "2026-11", 1000m, 5, AllConcepts(), Now);
        var shares = new[]
        {
            new TenantShare(Guid.NewGuid(), 400m),
            new TenantShare(Guid.NewGuid(), 400m)
        };

        Assert.Throws<ArgumentException>(() => period.ApplyCustomSplit(shares, Now));
    }

    [Fact]
    public void ApplyCustomSplit_SumaIgualALaRentaTotal_Aplica()
    {
        var period = RentPeriod.Configure(Guid.NewGuid(), "2026-11", 1000m, 5, AllConcepts(), Now);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var shares = new[]
        {
            new TenantShare(tenantA, 700m),
            new TenantShare(tenantB, 300m)
        };

        period.ApplyCustomSplit(shares, Now);

        Assert.Equal(RentSplitMethod.Personalizada, period.SplitMethod);
        Assert.Equal(700m, period.FindShare(tenantA)!.AmountMxn);
        Assert.Equal(300m, period.FindShare(tenantB)!.AmountMxn);
    }

    [Fact]
    public void ApplyCustomSplit_MontoNegativoOCero_DebeRechazarse()
    {
        var period = RentPeriod.Configure(Guid.NewGuid(), "2026-11", 1000m, 5, AllConcepts(), Now);

        Assert.Throws<ArgumentException>(() =>
            period.ApplyCustomSplit([new TenantShare(Guid.NewGuid(), 0m), new TenantShare(Guid.NewGuid(), 1000m)], Now));
        Assert.Throws<ArgumentException>(() =>
            period.ApplyCustomSplit([new TenantShare(Guid.NewGuid(), -100m), new TenantShare(Guid.NewGuid(), 1100m)], Now));
    }

    [Fact]
    public void InheritFrom_MismosInquilinosActivos_CopiaDivisionYCuotas()
    {
        var unitId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var previous = RentPeriod.Configure(unitId, "2026-10", 1000m, 31, AllConcepts(), Now);
        previous.ApplyEqualSplit([tenantA, tenantB], Now);

        var inherited = RentPeriod.InheritFrom(previous, "2026-11", [tenantA, tenantB], Now);

        Assert.Equal(unitId, inherited.UnitId);
        Assert.Equal("2026-11", inherited.Periodo);
        Assert.Equal(1000m, inherited.TotalMxn);
        Assert.Equal(31, inherited.CutoffDay);
        Assert.Equal(RentSplitMethod.Igual, inherited.SplitMethod);
        Assert.Equal(2, inherited.Shares.Count);
        Assert.NotEqual(previous.Id, inherited.Id);
        Assert.Equal(Now, inherited.CreatedAt);
        Assert.Equal(Now, inherited.UpdatedAt);
    }

    [Fact]
    public void InheritFrom_InquilinosDistintos_DejaDivisionPendiente()
    {
        var unitId = Guid.NewGuid();
        var previous = RentPeriod.Configure(unitId, "2026-10", 1000m, 31, AllConcepts(), Now);
        previous.ApplyEqualSplit([Guid.NewGuid(), Guid.NewGuid()], Now);

        var inherited = RentPeriod.InheritFrom(previous, "2026-11", [Guid.NewGuid()], Now);

        Assert.Equal(1000m, inherited.TotalMxn);
        Assert.True(inherited.IsSplitPending);
        Assert.Empty(inherited.Shares);
    }

    [Fact]
    public void InheritFrom_PeriodoAnteriorYaPendiente_SigueQuedandoPendiente()
    {
        var unitId = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var previous = RentPeriod.Configure(unitId, "2026-10", 1000m, 31, AllConcepts(), Now);

        var inherited = RentPeriod.InheritFrom(previous, "2026-11", [tenant], Now);

        Assert.True(inherited.IsSplitPending);
    }

    [Theory]
    [InlineData("2026-11", 31, 2026, 11, 30)]
    [InlineData("2026-02", 31, 2026, 2, 28)]
    [InlineData("2028-02", 31, 2028, 2, 29)]
    [InlineData("2026-01", 15, 2026, 1, 15)]
    public void ComputeCutoffDate_AjustaAlUltimoDiaRealDelMes(
        string periodo, int cutoffDay, int year, int month, int day)
    {
        var date = RentPeriod.ComputeCutoffDate(periodo, cutoffDay);

        Assert.Equal(new DateOnly(year, month, day), date);
    }

    [Fact]
    public void ComparePeriodo_OrdenaComoTexto()
    {
        Assert.True(RentPeriod.ComparePeriodo("2026-09", "2026-10") < 0);
        Assert.True(RentPeriod.ComparePeriodo("2026-10", "2026-10") == 0);
        Assert.True(RentPeriod.ComparePeriodo("2027-01", "2026-12") > 0);
    }

    [Theory]
    [InlineData(0, RentPaymentState.Pendiente)]
    [InlineData(500, RentPaymentState.Parcial)]
    [InlineData(1000, RentPaymentState.Registrado)]
    [InlineData(1500, RentPaymentState.Registrado)]
    public void ComputePaymentState_ClasificaSegunLoPagado(decimal totalPaid, RentPaymentState expected)
    {
        Assert.Equal(expected, RentPeriod.ComputePaymentState(1000m, totalPaid));
    }

    [Fact]
    public void InformativePayment_Register_ConDatosValidos()
    {
        var periodId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var payment = InformativePayment.Register(
            periodId, tenantId, 500m, DateOnly.FromDateTime(Now.UtcDateTime), Now);

        Assert.Equal(periodId, payment.RentPeriodId);
        Assert.Equal(tenantId, payment.TenantAccountId);
        Assert.Equal(500m, payment.AmountMxn);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InformativePayment_MontoNoPositivo_DebeRechazarse(decimal amount)
    {
        Assert.Throws<ArgumentException>(() =>
            InformativePayment.Register(
                Guid.NewGuid(), Guid.NewGuid(), amount, DateOnly.FromDateTime(Now.UtcDateTime), Now));
    }

    [Fact]
    public void InformativePayment_FechaFutura_DebeRechazarse()
    {
        var future = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(1);

        Assert.Throws<ArgumentException>(() =>
            InformativePayment.Register(Guid.NewGuid(), Guid.NewGuid(), 500m, future, Now));
    }

    [Fact]
    public void InformativePayment_IdsVacios_DebeRechazarse()
    {
        var today = DateOnly.FromDateTime(Now.UtcDateTime);

        Assert.Throws<ArgumentException>(() =>
            InformativePayment.Register(Guid.Empty, Guid.NewGuid(), 500m, today, Now));
        Assert.Throws<ArgumentException>(() =>
            InformativePayment.Register(Guid.NewGuid(), Guid.Empty, 500m, today, Now));
    }
}
