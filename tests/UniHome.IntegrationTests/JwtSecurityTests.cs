using System.Net;
using System.Net.Http.Headers;
using Microsoft.IdentityModel.Tokens;
using UniHome.Domain.Accounts;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

public sealed class JwtSecurityTests : IClassFixture<UniHomeApiFactory>
{
    private const string OtherSigningKey =
        "otra-clave-de-pruebas-0123456789-abcdefghijklmnopqrstuvwxyz";

    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public JwtSecurityTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Jwt_Expirado_DebeSerRechazado()
    {
        var account = await AuthTestHelpers.SeedAccountAsync(
            _factory,
            AuthTestHelpers.UniqueEmail());

        var token = JwtTestTokenFactory.Create(
            account.Id,
            Guid.NewGuid(),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            notBefore: DateTimeOffset.UtcNow.AddMinutes(-10));

        using var response = await LogoutAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Jwt_ConFirmaIncorrecta_DebeSerRechazado()
    {
        var account = await AuthTestHelpers.SeedAccountAsync(
            _factory,
            AuthTestHelpers.UniqueEmail());

        var token = JwtTestTokenFactory.Create(
            account.Id,
            Guid.NewGuid(),
            signingKey: OtherSigningKey);

        using var response = await LogoutAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Jwt_ConIssuerIncorrecto_DebeSerRechazado()
    {
        var account = await AuthTestHelpers.SeedAccountAsync(
            _factory,
            AuthTestHelpers.UniqueEmail());

        var token = JwtTestTokenFactory.Create(
            account.Id,
            Guid.NewGuid(),
            issuer: "Emisor.No.Confiable");

        using var response = await LogoutAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Jwt_ConAudienceIncorrecta_DebeSerRechazado()
    {
        var account = await AuthTestHelpers.SeedAccountAsync(
            _factory,
            AuthTestHelpers.UniqueEmail());

        var token = JwtTestTokenFactory.Create(
            account.Id,
            Guid.NewGuid(),
            audience: "Audiencia.No.Confiable");

        using var response = await LogoutAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Jwt_ConAlgoritmoDistintoAlEsperado_DebeSerRechazado()
    {
        var account = await AuthTestHelpers.SeedAccountAsync(
            _factory,
            AuthTestHelpers.UniqueEmail());

        var token = JwtTestTokenFactory.Create(
            account.Id,
            Guid.NewGuid(),
            algorithm: SecurityAlgorithms.HmacSha384);

        using var response = await LogoutAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Jwt_DeCuentaSuspendida_DebeSerRechazadoAunqueLaFirmaSeaValida()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        await _factory.Accounts.UpdateAsync(
            TestAccountFactory.WithStatus(account, AccountStatus.Suspended));

        using var response = await LogoutAsync(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Jwt_DeCuentaEliminada_DebeSerRechazadoAunqueLaFirmaSeaValida()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        await _factory.Accounts.UpdateAsync(
            TestAccountFactory.WithStatus(account, AccountStatus.Deleted));

        using var response = await LogoutAsync(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Jwt_DeCuentaInexistente_DebeSerRechazado()
    {
        var token = JwtTestTokenFactory.Create(Guid.NewGuid(), Guid.NewGuid());

        using var response = await LogoutAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private Task<HttpResponseMessage> LogoutAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }
}
