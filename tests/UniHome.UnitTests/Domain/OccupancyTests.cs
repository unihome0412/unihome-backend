using UniHome.Domain.OccupancyLinks;

namespace UniHome.UnitTests.Domain;

public sealed class OccupancyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OccupancyLink_CreateActive_QuedaActivoSinFin()
    {
        var tenantId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        var link = OccupancyLink.CreateActive(tenantId, unitId, Now);

        Assert.Equal(tenantId, link.TenantAccountId);
        Assert.Equal(unitId, link.UnitId);
        Assert.Equal(OccupancyStatus.Activo, link.Status);
        Assert.Equal(Now, link.JoinedAt);
        Assert.Null(link.EndedAt);
        Assert.Null(link.EndedReason);
    }

    [Fact]
    public void OccupancyLink_CreateActive_SinInquilinoOUnidad_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() =>
            OccupancyLink.CreateActive(Guid.Empty, Guid.NewGuid(), Now));
        Assert.Throws<ArgumentException>(() =>
            OccupancyLink.CreateActive(Guid.NewGuid(), Guid.Empty, Now));
    }

    [Fact]
    public void OccupancyLink_End_MarcaFinalizadoConMotivoYFecha()
    {
        var link = OccupancyLink.CreateActive(Guid.NewGuid(), Guid.NewGuid(), Now);
        var endedAt = Now.AddDays(10);

        link.End(OccupancyEndReason.AbandonoInquilino, endedAt);

        Assert.Equal(OccupancyStatus.Finalizado, link.Status);
        Assert.Equal(endedAt, link.EndedAt);
        Assert.Equal(OccupancyEndReason.AbandonoInquilino, link.EndedReason);
    }

    [Fact]
    public void OccupancyLink_End_SiYaEstaFinalizado_DebeRechazarse()
    {
        var link = OccupancyLink.CreateActive(Guid.NewGuid(), Guid.NewGuid(), Now);
        link.End(OccupancyEndReason.BajaPorPropietario, Now.AddDays(1));

        Assert.Throws<InvalidOperationException>(() =>
            link.End(OccupancyEndReason.BajaPorPropietario, Now.AddDays(2)));
    }

    [Fact]
    public void OccupancyLink_Reactivate_LimpiaFinYCambiaFechaDeVinculo()
    {
        var link = OccupancyLink.CreateActive(Guid.NewGuid(), Guid.NewGuid(), Now);
        link.End(OccupancyEndReason.AbandonoInquilino, Now.AddDays(1));

        var rejoinedAt = Now.AddDays(5);
        link.Reactivate(rejoinedAt);

        Assert.Equal(OccupancyStatus.Activo, link.Status);
        Assert.Equal(rejoinedAt, link.JoinedAt);
        Assert.Null(link.EndedAt);
        Assert.Null(link.EndedReason);
    }

    [Fact]
    public void OccupancyLink_Reactivate_SiYaEstaActivo_DebeRechazarse()
    {
        var link = OccupancyLink.CreateActive(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Throws<InvalidOperationException>(() => link.Reactivate(Now.AddDays(1)));
    }

    [Fact]
    public void UnitInvitation_Create_VenceALosSieteDias()
    {
        var unitId = Guid.NewGuid();

        var invitation = UnitInvitation.Create(unitId, "ABCD2345", Now);

        Assert.Equal(unitId, invitation.UnitId);
        Assert.Equal("ABCD2345", invitation.Code);
        Assert.Equal(Now, invitation.CreatedAt);
        Assert.Equal(Now.AddDays(UnitInvitation.ValidityDays), invitation.ExpiresAt);
        Assert.False(invitation.IsExpired(Now));
        Assert.True(invitation.IsExpired(Now.AddDays(UnitInvitation.ValidityDays)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC")]
    [InlineData("ABCDEFGHI")]
    public void UnitInvitation_Create_ConCodigoDeLongitudInvalida_DebeRechazarse(string code)
    {
        Assert.Throws<ArgumentException>(() => UnitInvitation.Create(Guid.NewGuid(), code, Now));
    }

    [Fact]
    public void UnitInvitation_Create_SinUnidad_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() => UnitInvitation.Create(Guid.Empty, "ABCD2345", Now));
    }
}
