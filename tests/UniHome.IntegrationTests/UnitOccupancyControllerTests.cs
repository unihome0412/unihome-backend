using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.OccupancyLinks;
using UniHome.Api.Controllers;
using UniHome.Application.OccupancyLinks;
using UniHome.Application.OccupancyLinks.JoinWithCode;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Units;
using UniHome.Infrastructure.Identity;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Cobertura del mapeo HTTP de la carrera de activación M09 sin depender de
/// MongoDB: el servicio devuelve <c>ConcurrentUpdate</c> y el controlador debe
/// responder 409 con ProblemDetails.
/// </summary>
public sealed class UnitOccupancyControllerTests
{
    [Fact]
    public async Task UnirseConCodigo_ActivacionConcurrente_Devuelve409()
    {
        var now = DateTimeOffset.UtcNow;
        var owner = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var unit = Unit.Create(
            Guid.NewGuid(),
            owner,
            Guid.NewGuid(),
            UnitType.Habitacion,
            Occupancy.Individual,
            1,
            2500,
            18,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            ["Cama"],
            null,
            now);
        var units = new InMemoryUnitRepository();
        await units.AddAsync(unit);
        var invitations = new InMemoryUnitInvitationRepository();
        await invitations.AddAsync(UnitInvitation.Create(unit.Id, "ABCD2345", now));
        var links = new InMemoryOccupancyLinkRepository { FailActivation = true };
        var service = new JoinUnitWithCodeService(
            invitations,
            units,
            links,
            new UnitAvailabilityRecalculator(units),
            new SystemClock());
        var controller = new UnitOccupancyController(
            null!,
            service,
            null!,
            null!,
            null!,
            null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("sub", tenant.ToString("N"))],
                        "Test"))
                }
            }
        };

        var result = await controller.JoinWithCode(
            new UnirseConCodigoRequest("ABCD2345"),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("modificado", problem.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, unit.OccupiedPlaces);
    }
}
