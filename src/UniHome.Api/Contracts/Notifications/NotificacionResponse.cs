using UniHome.Domain.Notifications;

namespace UniHome.Api.Contracts.Notifications;

public sealed record NotificacionResponse(
    Guid Id,
    NotificationType Tipo,
    string Mensaje,
    Guid? UnitId,
    string? Periodo,
    DateTimeOffset CreadoEn)
{
    public static NotificacionResponse FromDomain(Notification notification) =>
        new(
            notification.Id,
            notification.Type,
            notification.Message,
            notification.UnitId,
            notification.Periodo,
            notification.CreatedAt);
}
