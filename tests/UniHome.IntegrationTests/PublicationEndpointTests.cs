using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Accounts;
using UniHome.Domain.Units;
using UniHome.Domain.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

public sealed class PublicationEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private const string AdultEmail = "publication.adult@uni.edu";
    private const string OtherEmail = "publication.other@uni.edu";
    private const string MinorEmail = "publication.minor@uni.edu";

    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public PublicationEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FlujoCompleto_DebeCrearPropiedadUnidadFotosServiciosZonaVistaPreviaYTerminos()
    {
        var token = await AuthenticateAsync(AdultEmail);
        var universityId = await SeedUniversityAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var propertyId = await CreatePropertyAsync(universityId, "Casa Centro", "Av. Juárez 100");
        var unitId = await CreateUnitAsync(propertyId);
        await AddPhotosAsync(unitId);
        await ConfigureServicesAsync(unitId);
        await ConfigureZoneRulesAsync(unitId);

        using var preview = await _client.GetAsync($"/api/v1/unidades/{unitId}/vista-previa");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var previewJson = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        Assert.Equal("Borrador", previewJson.RootElement.GetProperty("estadoPublicacion").GetString());
        Assert.Equal(3500, previewJson.RootElement.GetProperty("precioMensualPorLugarMxn").GetDecimal());
        Assert.NotNull(previewJson.RootElement.GetProperty("fotoPrincipalUrl").GetString());
        Assert.Equal(JsonValueKind.Null, previewJson.RootElement.GetProperty("tiempoTraslado").ValueKind);

        using var terms = await _client.PostAsJsonAsync(
            "/api/v1/publicaciones/primera/terminos",
            new { versionTerminos = "2026-11" });
        Assert.Equal(HttpStatusCode.OK, terms.StatusCode);

        using var repeatedTerms = await _client.PostAsJsonAsync(
            "/api/v1/publicaciones/primera/terminos",
            new { versionTerminos = "2026-11" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, repeatedTerms.StatusCode);
    }

    [Fact]
    public async Task CrearPropiedad_Duplicada_DebeResponder409()
    {
        var token = await AuthenticateAsync(AdultEmail);
        var universityId = await SeedUniversityAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await CreatePropertyAsync(universityId, $"Casa {suffix}", $"Calle {suffix}");

        using var duplicate = await _client.PostAsJsonAsync(
            "/api/v1/propiedades",
            new
            {
                nombre = $"  CASA {suffix}  ",
                direccion = $"CALLE {suffix}",
                colonia = "Centro",
                tipoPropiedad = "Casa",
                universidadId = universityId,
                latitud = 19.4326,
                longitud = -99.1332
            });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task PropiedadEdificio_SinPisoNumero_DebeResponder400()
    {
        var token = await AuthenticateAsync(AdultEmail);
        var universityId = await SeedUniversityAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/propiedades",
            new
            {
                nombre = "Edificio",
                direccion = "Av. 1",
                colonia = "Centro",
                tipoPropiedad = "Edificio",
                universidadId = universityId,
                latitud = 19.4326,
                longitud = -99.1332
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PropiedadCasa_IgnoraPisoYNumero()
    {
        var token = await AuthenticateAsync(AdultEmail);
        var universityId = await SeedUniversityAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/propiedades",
            new
            {
                nombre = $"Casa {Guid.NewGuid():N}",
                direccion = "Av. 2",
                colonia = "Centro",
                tipoPropiedad = "Casa",
                universidadId = universityId,
                latitud = 19.4326,
                longitud = -99.1332,
                piso = "4",
                numeroUnidad = "402"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("piso").ValueKind);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("numeroUnidad").ValueKind);
    }

    [Fact]
    public async Task CrearUnidad_IndividualFuerzaUnLugarYPlantillaRellenaCampos()
    {
        var token = await AuthenticateAsync(AdultEmail);
        var universityId = await SeedUniversityAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var propertyId = await CreatePropertyAsync(universityId, "Casa Plantilla", "Calle Plantilla");

        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/propiedades/{propertyId}/unidades",
            new
            {
                tipoUnidad = "Habitacion",
                ocupacion = "Individual",
                lugares = 4,
                precioMensualPorLugarMxn = 3500,
                tamanoM2 = 12,
                plantillaBase = "HabitacionIndividual",
                muebles = new[] { "Cama" }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("lugares").GetInt32());
        Assert.Equal("Mixto", document.RootElement.GetProperty("paraQuien").GetString());
        Assert.Equal("Privado", document.RootElement.GetProperty("bano").GetString());
    }

    [Fact]
    public async Task OperacionesSobreRecursoAjeno_DebenResponder403()
    {
        var ownerToken = await AuthenticateAsync(AdultEmail);
        var universityId = await SeedUniversityAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, "Casa Owner", "Calle Owner");
        var unitId = await CreateUnitAsync(propertyId);

        var otherToken = await AuthenticateAsync(OtherEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        using var unit = await _client.PostAsJsonAsync(
            $"/api/v1/propiedades/{propertyId}/unidades",
            new
            {
                tipoUnidad = "Habitacion",
                ocupacion = "Individual",
                lugares = 1,
                precioMensualPorLugarMxn = 3000,
                tamanoM2 = 10,
                paraQuien = "Mixto",
                bano = "Privado",
                mobiliario = "Amueblado"
            });
        Assert.Equal(HttpStatusCode.Forbidden, unit.StatusCode);

        using var services = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/servicios",
            new { servicios = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Forbidden, services.StatusCode);

        using var zone = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/zona-reglas",
            new { informacionZona = "Zona", reglasConvivencia = "Reglas" });
        Assert.Equal(HttpStatusCode.Forbidden, zone.StatusCode);

        using var preview = await _client.GetAsync($"/api/v1/unidades/{unitId}/vista-previa");
        Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
    }

    [Fact]
    public async Task MenorDeEdad_NoPuedeCrearPropiedadNiAceptarTerminos()
    {
        var token = await AuthenticateAsync(MinorEmail, minor: true);
        var universityId = await SeedUniversityAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var property = await _client.PostAsJsonAsync(
            "/api/v1/propiedades",
            new
            {
                nombre = "Casa Menor",
                direccion = "Calle Menor",
                colonia = "Centro",
                tipoPropiedad = "Casa",
                universidadId = universityId,
                latitud = 19.4326,
                longitud = -99.1332
            });
        Assert.Equal(HttpStatusCode.Forbidden, property.StatusCode);

        using var terms = await _client.PostAsJsonAsync(
            "/api/v1/publicaciones/primera/terminos",
            new { versionTerminos = "2026-11" });
        Assert.Equal(HttpStatusCode.Forbidden, terms.StatusCode);
    }

    [Fact]
    public async Task Fotos_ConContenidoFalsoODemasiadas_DebenResponder400()
    {
        var token = await AuthenticateAsync(AdultEmail);
        var universityId = await SeedUniversityAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var propertyId = await CreatePropertyAsync(universityId, "Casa Fotos", "Calle Fotos");
        var unitId = await CreateUnitAsync(propertyId);

        using var invalid = await PostPhotosAsync(unitId, [([0x00, 0x01], "foto.jpg")]);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var tooMany = Enumerable.Range(0, Unit.MaxPhotos + 1)
            .Select(index => (PngBytes(), $"foto{index}.png"))
            .ToArray();
        using var tooManyResponse = await PostPhotosAsync(unitId, tooMany);
        Assert.Equal(HttpStatusCode.BadRequest, tooManyResponse.StatusCode);
    }

    [Fact]
    public async Task Servicios_AparteSinMonto_DebeResponder400()
    {
        var token = await AuthenticateAsync(AdultEmail);
        var universityId = await SeedUniversityAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var propertyId = await CreatePropertyAsync(universityId, "Casa Servicios", "Calle Servicios");
        var unitId = await CreateUnitAsync(propertyId);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/servicios",
            new
            {
                servicios = new object[]
                {
                    new { nombre = "Internet", modalidad = "Aparte" }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> AuthenticateAsync(string email, bool minor = false)
    {
        var existing = await _factory.Accounts.FindByEmailAsync(
            EmailAddress.Create(email));

        if (existing is null)
        {
            await AuthTestHelpers.SeedAccountAsync(
                _factory,
                email,
                birthDate: minor ? new DateOnly(2010, 1, 1) : null);
        }

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        return tokens.AccessToken;
    }

    private async Task<Guid> SeedUniversityAsync()
    {
        var university = University.Restore(
            Guid.NewGuid(),
            $"Universidad {Guid.NewGuid():N}",
            true,
            DateTimeOffset.UtcNow);
        await _factory.Universities.AddAsync(university);
        _factory.Universities.SetLocation(
            university.Id,
            new GeoCoordinate(19.4326, -99.1332));
        return university.Id;
    }

    private async Task<Guid> CreatePropertyAsync(
        Guid universityId,
        string name,
        string address)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/propiedades",
            new
            {
                nombre = name,
                direccion = address,
                colonia = "Centro",
                tipoPropiedad = "Casa",
                universidadId = universityId,
                latitud = 19.4326,
                longitud = -99.1332
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateUnitAsync(Guid propertyId)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/propiedades/{propertyId}/unidades",
            new
            {
                tipoUnidad = "Habitacion",
                ocupacion = "Individual",
                lugares = 1,
                precioMensualPorLugarMxn = 3500,
                tamanoM2 = 12,
                paraQuien = "Mixto",
                bano = "Privado",
                mobiliario = "Amueblado",
                muebles = new[] { "Cama" }
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private async Task AddPhotosAsync(Guid unitId)
    {
        using var response = await PostPhotosAsync(
            unitId,
            [(PngBytes(), "foto1.png"), (PngBytes(), "foto2.png")]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, document.RootElement.GetProperty("totalFotos").GetInt32());
    }

    private async Task<HttpResponseMessage> PostPhotosAsync(
        Guid unitId,
        IReadOnlyList<(byte[] Content, string FileName)> photos)
    {
        using var multipart = new MultipartFormDataContent();

        foreach (var (content, fileName) in photos)
        {
            var file = new ByteArrayContent(content);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            multipart.Add(file, "Fotos", fileName);
        }

        return await _client.PostAsync($"/api/v1/unidades/{unitId}/fotos", multipart);
    }

    private async Task ConfigureServicesAsync(Guid unitId)
    {
        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/servicios",
            new
            {
                servicios = new object[]
                {
                    new { nombre = "Agua", modalidad = "Incluido" },
                    new
                    {
                        nombre = "Internet",
                        modalidad = "Aparte",
                        montoMensualMxn = 300
                    }
                }
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, document.RootElement.GetProperty("servicios").GetArrayLength());
        Assert.Equal(
            300,
            document.RootElement.GetProperty("servicios")[1]
                .GetProperty("montoMensualMxn").GetDecimal());
    }

    private async Task ConfigureZoneRulesAsync(Guid unitId)
    {
        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/zona-reglas",
            new
            {
                informacionZona = "Cerca del metro",
                reglasConvivencia = "No fumar"
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Cerca del metro",
            document.RootElement.GetProperty("informacionZona").GetString());
    }

    private static byte[] PngBytes() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
}
