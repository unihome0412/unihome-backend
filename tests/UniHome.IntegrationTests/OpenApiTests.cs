using System.Net;
using System.Text.Json;

namespace UniHome.IntegrationTests;

public sealed class OpenApiTests : IClassFixture<SwaggerEnabledApiFactory>
{
    private readonly HttpClient _client;

    public OpenApiTests(SwaggerEnabledApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApi_DebeEstarDisponibleConSwaggerActivado()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("openapi", out _));
        Assert.True(root.TryGetProperty("paths", out _));
    }

    [Fact]
    public async Task OpenApi_DebeIncluirElRegistroDeM01()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/auth/registro", out var registro));
        Assert.True(registro.TryGetProperty("post", out _));
    }

    [Fact]
    public async Task OpenApi_DebeIncluirLosEndpointsNuevosDeM01()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/auth/confirmacion-correo", out var confirm));
        Assert.True(confirm.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty("/api/v1/auth/confirmacion-correo/reenvio", out var resend));
        Assert.True(resend.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty("/api/v1/cuentas/mi-cuenta", out var deleteAccount));
        Assert.True(deleteAccount.TryGetProperty("delete", out _));

        Assert.True(paths.TryGetProperty("/api/v1/auth/recuperacion/solicitud", out var resetRequest));
        Assert.True(resetRequest.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty("/api/v1/auth/recuperacion/confirmacion", out var resetConfirm));
        Assert.True(resetConfirm.TryGetProperty("post", out _));
    }

    [Fact]
    public async Task OpenApi_DebeIncluirLosEndpointsDeUniversidadesDeM03()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/admin/universidades", out var adminUniversities));
        Assert.True(adminUniversities.TryGetProperty("post", out _));
        Assert.True(adminUniversities.TryGetProperty("get", out _));

        Assert.True(paths.TryGetProperty("/api/v1/universidades", out var search));
        Assert.True(search.TryGetProperty("get", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/admin/universidades/{id}",
            out var editUniversity));
        Assert.True(editUniversity.TryGetProperty("put", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/admin/universidades/{id}/estado",
            out var changeUniversityStatus));
        Assert.True(changeUniversityStatus.TryGetProperty("put", out _));
    }

    [Fact]
    public async Task OpenApi_DebeIncluirLosEndpointsDeM05()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/propiedades", out var properties));
        Assert.True(properties.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/propiedades/{propertyId}/unidades",
            out var units));
        Assert.True(units.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/unidades/{unitId}/fotos",
            out var photos));
        Assert.True(photos.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/unidades/{unitId}/servicios",
            out var services));
        Assert.True(services.TryGetProperty("put", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/unidades/{unitId}/zona-reglas",
            out var zone));
        Assert.True(zone.TryGetProperty("put", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/unidades/{unitId}/vista-previa",
            out var preview));
        Assert.True(preview.TryGetProperty("get", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/publicaciones/primera/terminos",
            out var terms));
        Assert.True(terms.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/fotos/{photoRef}",
            out var photo));
        Assert.True(photo.TryGetProperty("get", out _));
    }

    [Fact]
    public async Task OpenApi_DebeIncluirLosEndpointsDeM04YM07()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/exploracion/mapa", out var map));
        Assert.True(map.TryGetProperty("get", out _));

        Assert.True(paths.TryGetProperty("/api/v1/exploracion/lista", out var list));
        Assert.True(list.TryGetProperty("get", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/unidades/{unitId}/tarjeta",
            out var card));
        Assert.True(card.TryGetProperty("get", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/unidades/{unitId}/ficha",
            out var detail));
        Assert.True(detail.TryGetProperty("get", out var detailGet));
        Assert.True(detailGet.TryGetProperty("security", out var detailSecurity));
        Assert.Contains(
            detailSecurity.EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));

        Assert.True(paths.TryGetProperty("/api/v1/favoritos", out var favorites));
        Assert.True(favorites.TryGetProperty("get", out var favoritesGet));
        Assert.True(favoritesGet.TryGetProperty("security", out var favoritesSecurity));
        Assert.Contains(
            favoritesSecurity.EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/favoritos/{unitId}",
            out var favoriteById));
        Assert.True(favoriteById.TryGetProperty("put", out _));
        Assert.True(favoriteById.TryGetProperty("delete", out _));

        Assert.True(paths.TryGetProperty("/api/v1/exploracion/mapa", out var publicMap));
        Assert.False(publicMap.GetProperty("get").TryGetProperty("security", out _));
    }

    [Fact]
    public async Task OpenApi_Exploracion_DebeDeclararContratoCamelCaseYEnumsString()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        var parameters = paths.GetProperty("/api/v1/exploracion/lista")
            .GetProperty("get")
            .GetProperty("parameters")
            .EnumerateArray()
            .ToDictionary(
                parameter => parameter.GetProperty("name").GetString()!,
                parameter => parameter,
                StringComparer.Ordinal);

        Assert.Equal(7, parameters.Count);
        foreach (var expected in new[]
                 {
                     "universidadId", "distanciaMaxKm", "precioMaxMxn", "soloIndividual",
                     "paraQuien", "page", "pageSize"
                 })
        {
            Assert.True(parameters.ContainsKey(expected), expected);
        }
        Assert.True(parameters["universidadId"].GetProperty("required").GetBoolean());
        Assert.Equal(
            "#/components/schemas/ForWhom",
            parameters["paraQuien"].GetProperty("schema").GetProperty("$ref").GetString());
        Assert.Equal(
            1,
            parameters["page"].GetProperty("schema").GetProperty("minimum").GetInt32());
        Assert.Equal(
            100,
            parameters["pageSize"].GetProperty("schema").GetProperty("maximum").GetInt32());

        var schemas = root.GetProperty("components").GetProperty("schemas");
        foreach (var enumName in new[]
                 {
                     "UnitType", "ForWhom", "Occupancy", "BathroomType", "FurnitureType",
                     "PropertyType"
                 })
        {
            var schema = schemas.GetProperty(enumName);
            Assert.Equal("string", schema.GetProperty("type").GetString());
            Assert.NotEmpty(schema.GetProperty("enum").EnumerateArray());
        }

        var card = schemas.GetProperty("UnidadTarjetaResponse").GetProperty("properties");
        Assert.Equal(
            "#/components/schemas/UnitType",
            card.GetProperty("tipoUnidad").GetProperty("$ref").GetString());
        Assert.Equal(
            "#/components/schemas/ForWhom",
            card.GetProperty("paraQuien").GetProperty("$ref").GetString());

        var detail = schemas.GetProperty("UnidadFichaResponse").GetProperty("properties");
        Assert.True(detail.TryGetProperty("tipoPropiedad", out _));
        Assert.True(detail.TryGetProperty("serviciosIncluidos", out _));
        Assert.True(detail.TryGetProperty("serviciosAparte", out _));
        Assert.True(detail.TryGetProperty("galeriaFotos", out _));
    }

    [Fact]
    public async Task OpenApi_DebeIncluirElPerfilDeM02()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/perfil/mi-perfil", out var profile));
        Assert.True(profile.TryGetProperty("get", out _));
    }

    [Fact]
    public async Task OpenApi_DebeIncluirLosEndpointsDeM06ConSeguridad()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        Assert.True(paths.TryGetProperty(
            "/api/v1/expedientes/mi-expediente/ine",
            out var ine));
        Assert.True(ine.TryGetProperty("post", out var inePost));
        Assert.Contains(
            inePost.GetProperty("security").EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/expedientes/mi-expediente/comprobante",
            out var proof));
        Assert.True(proof.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/publicaciones/primera/enviar",
            out var submit));
        Assert.True(submit.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty("/api/v1/admin/expedientes", out var adminList));
        Assert.True(adminList.TryGetProperty("get", out var adminListGet));
        Assert.Contains(
            adminListGet.GetProperty("security").EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/admin/expedientes/{id}",
            out var adminDetail));
        Assert.True(adminDetail.TryGetProperty("get", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/admin/expedientes/{id}/documentos/{tipo}",
            out var adminDocument));
        Assert.True(adminDocument.TryGetProperty("get", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/expedientes/documentos/{token}",
            out var signedDocument));
        Assert.True(signedDocument.TryGetProperty("get", out var signedGet));
        Assert.Contains(
            signedGet.GetProperty("security").EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));

        var schemas = root.GetProperty("components").GetProperty("schemas");

        foreach (var enumName in new[]
                 {
                     "EstadoExpediente", "TipoDocumentoVerificacion",
                     "ResultadoComparacionNombres"
                 })
        {
            var schema = schemas.GetProperty(enumName);
            Assert.Equal("string", schema.GetProperty("type").GetString());
            Assert.NotEmpty(schema.GetProperty("enum").EnumerateArray());
        }
    }
}
