using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Properties;
using UniHome.Domain.Rentals;
using UniHome.Domain.Reparaciones;
using UniHome.Domain.Units;

namespace UniHome.IntegrationTests;

/// <summary>EP-M05/M10/M11 (HU-034): pantalla de Resumen del titular.</summary>
public sealed class SummaryEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public SummaryEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MiResumen_SinPropiedades_DebeResponderEnCeros()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var response = await _client.GetAsync("/api/v1/mi-resumen");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(0m, root.GetProperty("rentasRegistradasDelMesMxn").GetDecimal());
        Assert.Equal(0, root.GetProperty("numeroPropiedades").GetInt32());
        Assert.Equal(0, root.GetProperty("reparacionesAbiertas").GetInt32());
    }

    [Fact]
    public async Task MiResumen_ConPropiedadRentaYReparaciones_DebeAgregarLosTresIndicadores()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var now = DateTimeOffset.UtcNow;
        var periodo = now.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        var property = Property.Create(
            account.Id, "Casa Resumen", "Calle 1", "Centro", PropertyType.Casa,
            Guid.NewGuid(), latitude: null, longitude: null, floor: null, unitNumber: null, now);
        await _factory.Properties.AddAsync(property);

        var unit = Unit.Create(
            property.Id, account.Id, Guid.NewGuid(), UnitType.Habitacion, Occupancy.Individual,
            1, 1500m, 12m, ForWhom.Mixto, BathroomType.Compartido, FurnitureType.SinMuebles,
            furnitureItems: null, template: null, now);
        await _factory.Units.AddAsync(unit);

        var period = RentPeriod.Configure(
            unit.Id, periodo, 1000m, 5,
            [
                new RentConcept(RentConceptName.Renta, true),
                new RentConcept(RentConceptName.Mantenimiento, true),
                new RentConcept(RentConceptName.Luz, false),
                new RentConcept(RentConceptName.Gas, false)
            ],
            now);
        await _factory.RentPeriods.SaveAsync(period);

        var payment = InformativePayment.Register(
            period.Id, Guid.NewGuid(), 600m, DateOnly.FromDateTime(now.UtcDateTime), now);
        await _factory.InformativePayments.SaveAsync(payment);

        var abierto = Reparacion.Create(unit.Id, account.Id, "Fuga", "Hay una fuga", photo: null, now);
        var atendido = Reparacion.Create(unit.Id, account.Id, "Foco", "Foco fundido", photo: null, now);
        atendido.ChangeStatus(ReparacionEstado.Atendido, now);
        await _factory.Reparaciones.AddAsync(abierto);
        await _factory.Reparaciones.AddAsync(atendido);

        using var response = await _client.GetAsync("/api/v1/mi-resumen");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(periodo, root.GetProperty("periodo").GetString());
        Assert.Equal(600m, root.GetProperty("rentasRegistradasDelMesMxn").GetDecimal());
        Assert.Equal(1, root.GetProperty("numeroPropiedades").GetInt32());
        Assert.Equal(1, root.GetProperty("reparacionesAbiertas").GetInt32());
    }

    [Fact]
    public async Task MiResumen_SinSesion_DebeResponder401()
    {
        using var response = await _client.GetAsync("/api/v1/mi-resumen");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
