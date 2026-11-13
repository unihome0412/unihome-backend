using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Domain.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

public sealed class AdminUniversitiesEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private const string AdminEmail = "admin.universidades@uni.edu";

    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public AdminUniversitiesEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Crear_ComoAdmin_DebeResponder201YAparecerDeInmediatoEnBusqueda()
    {
        await AuthenticateAsAdminAsync();
        var name = $"Universidad Tecnológica {Guid.NewGuid():N}";

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/universidades",
            new { nombreYCampus = name });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var id = root.GetProperty("id").GetGuid();
        Assert.Equal(name, root.GetProperty("nombreYCampus").GetString());
        Assert.True(root.GetProperty("activa").GetBoolean());
        Assert.True(root.GetProperty("fechaAlta").GetDateTimeOffset() > DateTimeOffset.MinValue);

        using var search = await _client.GetAsync(
            $"/api/v1/universidades?buscar={Uri.EscapeDataString(name)}");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);

        using var searchDocument = JsonDocument.Parse(await search.Content.ReadAsStringAsync());
        Assert.Contains(
            searchDocument.RootElement.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == id &&
                    item.GetProperty("nombreYCampus").GetString() == name);
    }

    [Fact]
    public async Task Crear_SinToken_DebeResponder401()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/universidades",
            new { nombreYCampus = "Universidad Sin Token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Crear_ConUsuarioSinPrivilegioAdmin_DebeResponder403()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/universidades",
            new { nombreYCampus = "Universidad No Autorizada" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Crear_ConClaimAdminFalso_DebeResponder403()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);
        var token = JwtTestTokenFactory.Create(
            account.Id,
            Guid.NewGuid(),
            includeAdminClaim: true);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/universidades",
            new { nombreYCampus = "Universidad con Claim Falso" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Crear_ConNombreYCampusVacio_DebeResponder400(string? value)
    {
        await AuthenticateAsAdminAsync();

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/universidades",
            new { nombreYCampus = value });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement
            .GetProperty("errors")
            .TryGetProperty("nombreYCampus", out _));
    }

    [Fact]
    public async Task Crear_ConLongitudExcesiva_DebeResponder400()
    {
        await AuthenticateAsAdminAsync();

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/universidades",
            new { nombreYCampus = new string('a', University.MaxNameAndCampusLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Crear_DuplicadoNormalizado_DebeResponder409()
    {
        await AuthenticateAsAdminAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var original = $"Universidad Autónoma {suffix}";

        using var first = await _client.PostAsJsonAsync(
            "/api/v1/admin/universidades",
            new { nombreYCampus = original });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var duplicate = await _client.PostAsJsonAsync(
            "/api/v1/admin/universidades",
            new { nombreYCampus = $"  UNIVERSIDAD AUTONOMA {suffix}  " });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Single(
            _factory.Universities.All,
            university => university.NormalizedName == University.Normalize(original));
    }

    [Fact]
    public async Task Crear_ConCuerpoExcesivo_DebeResponder413()
    {
        await AuthenticateAsAdminAsync();

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/universidades",
            new { nombreYCampus = new string('a', 16 * 1024) });

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Buscar_DebeIgnorarAcentosMayusculasYLimitarA10()
    {
        for (var index = 0; index < 15; index++)
        {
            await _factory.Universities.AddAsync(University.Restore(
                Guid.NewGuid(),
                $"Universidad Tecnológica {index:D2}",
                isActive: true,
                DateTimeOffset.UtcNow));
        }

        await _factory.Universities.AddAsync(University.Restore(
            Guid.NewGuid(),
            "Universidad Tecnológica Inactiva",
            isActive: false,
            DateTimeOffset.UtcNow));

        using var response = await _client.GetAsync(
            "/api/v1/universidades?buscar=TECNOL%C3%93GICA");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var results = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(10, results.Length);
        Assert.DoesNotContain(
            results,
            item => item.GetProperty("nombreYCampus").GetString()!.Contains(
                "Inactiva",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Buscar_ConTerminoExcesivo_DebeResponder400()
    {
        using var response = await _client.GetAsync(
            $"/api/v1/universidades?buscar={new string('a', 101)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement
            .GetProperty("errors")
            .TryGetProperty("buscar", out _));
    }

    [Fact]
    public async Task Buscar_SinCoincidencias_DebeResponderListaVacia()
    {
        using var response = await _client.GetAsync(
            "/api/v1/universidades?buscar=zzz-sin-coincidencias");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(document.RootElement.EnumerateArray());
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var existing = await _factory.Accounts.FindByIdAsync(
            UniHomeApiFactory.TestAdminAccountId);

        var admin = existing ?? await AuthTestHelpers.SeedAccountAsync(
            _factory,
            AdminEmail,
            id: UniHomeApiFactory.TestAdminAccountId);

        using var login = await AuthTestHelpers.LoginAsync(_client, admin.Email.Value);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }
}
