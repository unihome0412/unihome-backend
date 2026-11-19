using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Properties;
using UniHome.Application.Properties.MyProperties;

namespace UniHome.Api.Controllers;

[ApiController]
public sealed class MyPropertiesController : AuthenticatedControllerBase
{
    private readonly GetMyPropertiesService _getMyProperties;

    public MyPropertiesController(GetMyPropertiesService getMyProperties)
    {
        _getMyProperties = getMyProperties;
    }

    /// <summary>
    /// EP-M05-11 (HU-033): propiedades del titular con tipo, total de
    /// habitaciones, cuántas están disponibles y el estado de cada una.
    /// Filtra por tipo de propiedad y por el estado de al menos una habitación.
    /// </summary>
    [HttpGet("/api/v1/mis-propiedades")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<MiPropiedadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List(
        [FromQuery] MisPropiedadesQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var views = await _getMyProperties
            .ListAsync(accountId, query.TipoPropiedad, query.EstadoHabitacion, cancellationToken)
            .ConfigureAwait(false);

        return Ok(views.Select(MiPropiedadResponse.FromView).ToArray());
    }
}
