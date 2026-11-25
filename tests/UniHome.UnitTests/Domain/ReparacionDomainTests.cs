using UniHome.Domain.Reparaciones;

namespace UniHome.UnitTests.Domain;

public sealed class ReparacionDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ConDatosValidos_QuedaPendienteYSinFoto()
    {
        var unitId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var reparacion = Reparacion.Create(unitId, reporterId, "Fuga", "Hay una fuga", photo: null, Now);

        Assert.Equal(unitId, reparacion.UnitId);
        Assert.Equal(reporterId, reparacion.ReportedByAccountId);
        Assert.Equal("Fuga", reparacion.Title);
        Assert.Equal("Hay una fuga", reparacion.Description);
        Assert.Equal(ReparacionEstado.Pendiente, reparacion.Status);
        Assert.Null(reparacion.Photo);
        Assert.Equal(Now, reparacion.CreatedAt);
        Assert.Equal(Now, reparacion.UpdatedAt);
    }

    [Fact]
    public void Create_ConFoto_LaConserva()
    {
        var photo = new ReparacionFoto("key", "image/png", 1024, "hash");

        var reparacion = Reparacion.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Foco", "Fundido", photo, Now);

        Assert.Same(photo, reparacion.Photo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ConTituloInvalido_DebeRechazarse(string? title)
    {
        Assert.Throws<ArgumentException>(
            () => Reparacion.Create(Guid.NewGuid(), Guid.NewGuid(), title, "Descripción", null, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ConDescripcionInvalida_DebeRechazarse(string? description)
    {
        Assert.Throws<ArgumentException>(
            () => Reparacion.Create(Guid.NewGuid(), Guid.NewGuid(), "Título", description, null, Now));
    }

    [Fact]
    public void Create_ConTituloDemasiadoLargo_DebeRechazarse()
    {
        var title = new string('a', Reparacion.MaxTitleLength + 1);

        Assert.Throws<ArgumentException>(
            () => Reparacion.Create(Guid.NewGuid(), Guid.NewGuid(), title, "Descripción", null, Now));
    }

    [Fact]
    public void Create_ConDescripcionDemasiadoLarga_DebeRechazarse()
    {
        var description = new string('a', Reparacion.MaxDescriptionLength + 1);

        Assert.Throws<ArgumentException>(
            () => Reparacion.Create(Guid.NewGuid(), Guid.NewGuid(), "Título", description, null, Now));
    }

    [Fact]
    public void Create_ConUnidadVacia_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(
            () => Reparacion.Create(Guid.Empty, Guid.NewGuid(), "Título", "Descripción", null, Now));
    }

    [Fact]
    public void Create_ConReportanteVacio_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(
            () => Reparacion.Create(Guid.NewGuid(), Guid.Empty, "Título", "Descripción", null, Now));
    }

    [Fact]
    public void ChangeStatus_ConEstadoDistinto_ActualizaEstadoYFechaYDevuelveTrue()
    {
        var reparacion = Reparacion.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Título", "Descripción", null, Now);
        var later = Now.AddHours(2);

        var changed = reparacion.ChangeStatus(ReparacionEstado.EnProceso, later);

        Assert.True(changed);
        Assert.Equal(ReparacionEstado.EnProceso, reparacion.Status);
        Assert.Equal(later, reparacion.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_ConElMismoEstado_NoCambiaNadaYDevuelveFalse()
    {
        var reparacion = Reparacion.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Título", "Descripción", null, Now);

        var changed = reparacion.ChangeStatus(ReparacionEstado.Pendiente, Now.AddHours(2));

        Assert.False(changed);
        Assert.Equal(ReparacionEstado.Pendiente, reparacion.Status);
        Assert.Equal(Now, reparacion.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_PermiteVolverAUnEstadoAnterior()
    {
        var reparacion = Reparacion.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Título", "Descripción", null, Now);
        reparacion.ChangeStatus(ReparacionEstado.Atendido, Now.AddHours(1));

        var changed = reparacion.ChangeStatus(ReparacionEstado.EnProceso, Now.AddHours(2));

        Assert.True(changed);
        Assert.Equal(ReparacionEstado.EnProceso, reparacion.Status);
    }

    [Fact]
    public void ChangeStatus_ConEstadoNoDefinido_DebeRechazarse()
    {
        var reparacion = Reparacion.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Título", "Descripción", null, Now);

        Assert.Throws<ArgumentException>(() => reparacion.ChangeStatus((ReparacionEstado)99, Now));
    }

    [Fact]
    public void Restore_ConIdentificadorVacio_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() => Reparacion.Restore(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "Título", "Descripción",
            ReparacionEstado.Pendiente, null, Now, Now));
    }

    [Fact]
    public void Restore_ConEstadoNoDefinido_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() => Reparacion.Restore(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Título", "Descripción",
            (ReparacionEstado)99, null, Now, Now));
    }

    [Fact]
    public void Restore_ConDatosValidos_ReconstruyeLaEntidad()
    {
        var id = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var photo = new ReparacionFoto("key", "image/jpeg", 2048, "hash");

        var reparacion = Reparacion.Restore(
            id, unitId, reporterId, "Título", "Descripción",
            ReparacionEstado.EnProceso, photo, Now, Now.AddHours(1));

        Assert.Equal(id, reparacion.Id);
        Assert.Equal(unitId, reparacion.UnitId);
        Assert.Equal(reporterId, reparacion.ReportedByAccountId);
        Assert.Equal(ReparacionEstado.EnProceso, reparacion.Status);
        Assert.Same(photo, reparacion.Photo);
        Assert.Equal(Now.AddHours(1), reparacion.UpdatedAt);
    }
}
