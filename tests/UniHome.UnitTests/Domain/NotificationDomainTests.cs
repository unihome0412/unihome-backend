using UniHome.Domain.Notifications;

namespace UniHome.UnitTests.Domain;

public sealed class NotificationDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateRentReminder_ConDatosValidos()
    {
        var recipient = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        var notification = Notification.CreateRentReminder(
            recipient, unitId, "2026-11", "Tu renta vence pronto.", Now);

        Assert.Equal(recipient, notification.RecipientAccountId);
        Assert.Equal(NotificationType.RecordatorioRenta, notification.Type);
        Assert.Equal(unitId, notification.UnitId);
        Assert.Equal("2026-11", notification.Periodo);
        Assert.Equal("Tu renta vence pronto.", notification.Message);
    }

    [Fact]
    public void ComputeRentReminderId_MismaCombinacion_MismoId()
    {
        var recipient = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        var first = Notification.ComputeRentReminderId(recipient, unitId, "2026-11");
        var second = Notification.ComputeRentReminderId(recipient, unitId, "2026-11");

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeRentReminderId_PeriodoDistinto_IdDistinto()
    {
        var recipient = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        var november = Notification.ComputeRentReminderId(recipient, unitId, "2026-11");
        var december = Notification.ComputeRentReminderId(recipient, unitId, "2026-12");

        Assert.NotEqual(november, december);
    }

    [Fact]
    public void CreateRentReminder_DestinatarioOUnidadVacios_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() =>
            Notification.CreateRentReminder(Guid.Empty, Guid.NewGuid(), "2026-11", "msg", Now));
        Assert.Throws<ArgumentException>(() =>
            Notification.CreateRentReminder(Guid.NewGuid(), Guid.Empty, "2026-11", "msg", Now));
    }

    [Fact]
    public void CreateRentReminder_MensajeVacio_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() =>
            Notification.CreateRentReminder(Guid.NewGuid(), Guid.NewGuid(), "2026-11", "   ", Now));
    }
}
