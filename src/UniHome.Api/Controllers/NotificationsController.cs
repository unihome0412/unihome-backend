using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Notifications;
using UniHome.Application.Notifications;

namespace UniHome.Api.Controllers;

/// <summary>
/// EP-M12-01 (HU-059): recordatorio de renta. Se genera al consultar este
/// endpoint, nunca por un job en segundo plano.
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class NotificationsController : AuthenticatedControllerBase
{
    private readonly ListMyNotificationsService _listNotifications;

    public NotificationsController(ListMyNotificationsService listNotifications)
    {
        _listNotifications = listNotifications;
    }

    [HttpGet("notificaciones")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<NotificacionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var notifications = await _listNotifications
            .ListAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(notifications.Select(NotificacionResponse.FromDomain).ToArray());
    }
}
