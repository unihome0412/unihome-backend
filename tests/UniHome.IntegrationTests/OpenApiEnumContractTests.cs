using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Application.Profiles.GetProfile;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Accounts;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.Domain.Universities;

namespace UniHome.IntegrationTests;

/// <summary>
/// El contrato OpenAPI debe describir exactamente los enums string que acepta y
/// devuelve el runtime. Cubre los 14 enums públicos usados por request/response.
/// </summary>
public sealed class OpenApiEnumContractTests : IClassFixture<SwaggerEnabledApiFactory>
{
    private static readonly IReadOnlyDictionary<string, Type> PublicEnums =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["Gender"] = typeof(Gender),
            ["PropertyType"] = typeof(PropertyType),
            ["PublicationState"] = typeof(PublicationState),
            ["UnitType"] = typeof(UnitType),
            ["Occupancy"] = typeof(Occupancy),
            ["ForWhom"] = typeof(ForWhom),
            ["BathroomType"] = typeof(BathroomType),
            ["FurnitureType"] = typeof(FurnitureType),
            ["UnitTemplate"] = typeof(UnitTemplate),
            ["ServiceName"] = typeof(ServiceName),
            ["ServiceMode"] = typeof(ServiceMode),
            ["AccountLabel"] = typeof(AccountLabel),
            ["UnitAvailability"] = typeof(UnitAvailability),
            ["OccupancyStatus"] = typeof(OccupancyStatus)
        };

    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public OpenApiEnumContractTests(SwaggerEnabledApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAdminClient();
    }

    [Fact]
    public async Task OpenApi_TodosLosEnumsPublicosSonStringConSusValoresReales()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schemas = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas");

        foreach (var (name, enumType) in PublicEnums)
        {
            var schema = schemas.GetProperty(name);
            Assert.Equal("string", schema.GetProperty("type").GetString());

            var actual = schema.GetProperty("enum")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray();

            Assert.Equal(Enum.GetNames(enumType), actual);
        }
    }

    [Fact]
    public async Task OpenApi_ReferenciaLosEnumsEnRequestYResponse()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schemas = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas");

        AssertReferences(schemas, "RegistroRequest", "genero", "Gender", nullable: true);
        AssertReferences(schemas, "PerfilResponse", "genero", "Gender");
        AssertReferences(schemas, "PerfilResponse", "etiquetaCuenta", "AccountLabel");

        AssertReferences(schemas, "CrearPropiedadRequest", "tipoPropiedad", "PropertyType");
        AssertReferences(schemas, "PropiedadResponse", "tipoPropiedad", "PropertyType");
        AssertReferences(schemas, "PropiedadResponse", "estadoPublicacion", "PublicationState");

        AssertReferences(schemas, "CrearUnidadRequest", "tipoUnidad", "UnitType", nullable: true);
        AssertReferences(schemas, "CrearUnidadRequest", "ocupacion", "Occupancy", nullable: true);
        AssertReferences(schemas, "CrearUnidadRequest", "paraQuien", "ForWhom", nullable: true);
        AssertReferences(schemas, "CrearUnidadRequest", "bano", "BathroomType", nullable: true);
        AssertReferences(schemas, "CrearUnidadRequest", "mobiliario", "FurnitureType", nullable: true);
        AssertReferences(schemas, "CrearUnidadRequest", "plantillaBase", "UnitTemplate", nullable: true);

        AssertReferences(schemas, "UnidadResponse", "tipoUnidad", "UnitType");
        AssertReferences(schemas, "UnidadResponse", "ocupacion", "Occupancy");
        AssertReferences(schemas, "UnidadResponse", "paraQuien", "ForWhom");
        AssertReferences(schemas, "UnidadResponse", "bano", "BathroomType");
        AssertReferences(schemas, "UnidadResponse", "mobiliario", "FurnitureType");
        AssertReferences(schemas, "UnidadResponse", "plantillaBase", "UnitTemplate", nullable: true);
        AssertReferences(schemas, "UnidadResponse", "estadoPublicacion", "PublicationState");

        AssertReferences(schemas, "ServicioInput", "nombre", "ServiceName");
        AssertReferences(schemas, "ServicioInput", "modalidad", "ServiceMode");
        AssertReferences(schemas, "ServicioResponse", "nombre", "ServiceName");
        AssertReferences(schemas, "ServicioResponse", "modalidad", "ServiceMode");

        AssertReferences(schemas, "UnidadVistaPreviaResponse", "tipoUnidad", "UnitType");
        AssertReferences(schemas, "UnidadVistaPreviaResponse", "paraQuien", "ForWhom");
        AssertReferences(schemas, "UnidadVistaPreviaResponse", "estadoPublicacion", "PublicationState");

        AssertReferences(schemas, "MiPropiedadResponse", "tipoPropiedad", "PropertyType");
        AssertReferences(schemas, "MiHabitacionResponse", "tipoUnidad", "UnitType");
        AssertReferences(schemas, "MiHabitacionResponse", "estado", "UnitAvailability");

        AssertReferences(schemas, "UnidadResponse", "estadoOcupacion", "UnitAvailability");
        AssertReferences(
            schemas,
            "UnidadVistaPreviaResponse",
            "estadoOcupacion",
            "UnitAvailability");
        AssertReferences(schemas, "VinculoOcupacionResponse", "estado", "OccupancyStatus");
    }

    [Fact]
    public async Task Runtime_AceptaEnumsStringYRechazaEnterosEnElMismoContrato()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var university = University.Restore(
            Guid.NewGuid(),
            $"Universidad {Guid.NewGuid():N}",
            true,
            DateTimeOffset.UtcNow);
        await _factory.Universities.AddAsync(university);
        _factory.Universities.SetLocation(
            university.Id,
            new GeoCoordinate(19.4326, -99.1332));

        using var propertyResponse = await _client.PostAsJsonAsync(
            "/api/v1/propiedades",
            new
            {
                nombre = $"Casa {Guid.NewGuid():N}",
                direccion = "Calle Enum 1",
                colonia = "Centro",
                tipoPropiedad = "Casa",
                universidadId = university.Id,
                latitud = 19.4326,
                longitud = -99.1332
            });
        Assert.Equal(HttpStatusCode.Created, propertyResponse.StatusCode);
        using var propertyJson = JsonDocument.Parse(
            await propertyResponse.Content.ReadAsStringAsync());
        var propertyId = propertyJson.RootElement.GetProperty("id").GetGuid();

        var validUnit = new
        {
            tipoUnidad = "Habitacion",
            ocupacion = "Compartida",
            lugares = 2,
            precioMensualPorLugarMxn = 3500,
            tamanoM2 = 15,
            paraQuien = "Mixto",
            bano = "Compartido",
            mobiliario = "Amueblado",
            muebles = new[] { "Cama" },
            plantillaBase = "HabitacionCompartida"
        };

        using var accepted = await _client.PostAsJsonAsync(
            $"/api/v1/propiedades/{propertyId}/unidades",
            validUnit);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        using var acceptedJson = JsonDocument.Parse(
            await accepted.Content.ReadAsStringAsync());
        Assert.Equal("Habitacion", acceptedJson.RootElement.GetProperty("tipoUnidad").GetString());
        Assert.Equal("Compartida", acceptedJson.RootElement.GetProperty("ocupacion").GetString());
        Assert.Equal("Mixto", acceptedJson.RootElement.GetProperty("paraQuien").GetString());
        Assert.Equal("Compartido", acceptedJson.RootElement.GetProperty("bano").GetString());
        Assert.Equal("Amueblado", acceptedJson.RootElement.GetProperty("mobiliario").GetString());
        Assert.Equal(
            "HabitacionCompartida",
            acceptedJson.RootElement.GetProperty("plantillaBase").GetString());

        using var rejected = await _client.PostAsJsonAsync(
            $"/api/v1/propiedades/{propertyId}/unidades",
            new
            {
                tipoUnidad = 1,
                ocupacion = "Compartida",
                lugares = 2,
                precioMensualPorLugarMxn = 3500,
                tamanoM2 = 15,
                paraQuien = "Mixto",
                bano = "Compartido",
                mobiliario = "Amueblado",
                muebles = new[] { "Cama" }
            });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        Assert.NotEqual(Guid.Empty, account.Id);
    }

    private static void AssertReferences(
        JsonElement schemas,
        string schemaName,
        string propertyName,
        string enumName,
        bool nullable = false)
    {
        var property = schemas
            .GetProperty(schemaName)
            .GetProperty("properties")
            .GetProperty(propertyName);

        if (property.TryGetProperty("$ref", out var reference))
        {
            Assert.Equal($"#/components/schemas/{enumName}", reference.GetString());
            Assert.False(nullable);
            return;
        }

        Assert.True(nullable, $"{schemaName}.{propertyName} no referencia {enumName} directamente.");
        Assert.True(property.TryGetProperty("oneOf", out var oneOf));

        Assert.Contains(
            oneOf.EnumerateArray(),
            candidate => candidate.TryGetProperty("$ref", out var candidateRef) &&
                         candidateRef.GetString() == $"#/components/schemas/{enumName}");
    }
}
