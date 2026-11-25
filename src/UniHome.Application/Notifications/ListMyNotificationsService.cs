using System.Globalization;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Notifications.Abstractions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Rentals;
using UniHome.Application.Rentals.Abstractions;
using UniHome.Domain.Notifications;
using UniHome.Domain.Rentals;

namespace UniHome.Application.Notifications;

/// <summary>
/// EP-M12-01 (HU-059): genera el recordatorio de renta del inquilino al
/// consultar <c>GET /notificaciones</c> (nunca un job en segundo plano) y
/// devuelve todas sus notificaciones. Por cada unidad donde la cuenta es
/// inquilino activo, si el periodo del mes en curso ya tiene cuota asignada,
/// al corte le quedan entre 0 y 3 días y la cuota todavía no está cubierta,
/// genera o renueva el recordatorio (<see cref="Notification.CreateRentReminder"/>
/// es un upsert idempotente por su id determinista: volver a generarlo en la
/// misma ventana no crea un duplicado).
/// </summary>
public sealed class ListMyNotificationsService
{
    private const int ReminderWindowDays = 3;

    private readonly IOccupancyLinkRepository _links;
    private readonly RentPeriodResolver _resolver;
    private readonly IInformativePaymentRepository _payments;
    private readonly INotificationRepository _notifications;
    private readonly ISystemClock _clock;

    public ListMyNotificationsService(
        IOccupancyLinkRepository links,
        RentPeriodResolver resolver,
        IInformativePaymentRepository payments,
        INotificationRepository notifications,
        ISystemClock clock)
    {
        _links = links;
        _resolver = resolver;
        _payments = payments;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Notification>> ListAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var currentPeriodo = today.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        var activeLinks = await _links
            .ListActiveByTenantAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var link in activeLinks)
        {
            var period = await _resolver
                .ResolveAsync(link.UnitId, currentPeriodo, cancellationToken)
                .ConfigureAwait(false);
            var share = period?.FindShare(accountId);

            if (period is null || share is null)
            {
                continue;
            }

            var cutoffDate = RentPeriod.ComputeCutoffDate(currentPeriodo, period.CutoffDay);
            var daysUntilCutoff = cutoffDate.DayNumber - today.DayNumber;

            if (daysUntilCutoff < 0 || daysUntilCutoff > ReminderWindowDays)
            {
                continue;
            }

            var paid = (await _payments
                .ListByRentPeriodAsync(period.Id, cancellationToken)
                .ConfigureAwait(false))
                .Where(payment => payment.TenantAccountId == accountId)
                .Sum(payment => payment.AmountMxn);

            if (RentPeriod.ComputePaymentState(share.AmountMxn, paid) == RentPaymentState.Registrado)
            {
                continue;
            }

            var message = daysUntilCutoff == 0
                ? $"Tu renta del periodo {currentPeriodo} vence hoy ({cutoffDate:dd/MM/yyyy})."
                : $"Tu renta del periodo {currentPeriodo} vence el {cutoffDate:dd/MM/yyyy} " +
                  $"(en {daysUntilCutoff} día{(daysUntilCutoff == 1 ? "" : "s")}).";

            var reminder = Notification.CreateRentReminder(
                accountId, link.UnitId, currentPeriodo, message, now);
            await _notifications.SaveAsync(reminder, cancellationToken).ConfigureAwait(false);
        }

        return await _notifications
            .ListByRecipientAsync(accountId, cancellationToken)
            .ConfigureAwait(false);
    }
}
