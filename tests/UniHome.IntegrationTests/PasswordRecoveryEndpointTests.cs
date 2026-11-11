using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.IntegrationTests;

public sealed class PasswordRecoveryEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public PasswordRecoveryEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.PasswordResets.Clear();
        _factory.PasswordResetEmailSender.Clear();
        _factory.Logs.Clear();
    }

    [Fact]
    public async Task Solicitar_ConCorreoExistente_DebeResponderMensajeGenericoYEnviarEnlace()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        _factory.Logs.Clear();

        using var response = await RequestResetAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Si el correo está registrado, recibirás instrucciones para restablecer tu contraseña",
            document.RootElement.GetProperty("mensaje").GetString());

        var sent = Assert.Single(_factory.PasswordResetEmailSender.Messages);
        Assert.Equal(email, sent.Email);
        var stored = Assert.Single(_factory.PasswordResets.All);
        Assert.NotEqual(sent.Token, stored.TokenHash);
        Assert.Equal(
            _factory.Services.GetRequiredService<IPasswordResetTokenGenerator>().Hash(sent.Token),
            stored.TokenHash);
        Assert.True(stored.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.True(stored.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(31));

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sent.Token, body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            _factory.Logs.Messages,
            message => message.Contains(sent.Token, StringComparison.Ordinal));
        Assert.DoesNotContain(
            _factory.Logs.Messages,
            message => message.Contains(email, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Solicitar_ConCorreoInexistente_DebeResponderIgualSinEnviarCorreo()
    {
        using var response = await RequestResetAsync(AuthTestHelpers.UniqueEmail());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Si el correo está registrado, recibirás instrucciones para restablecer tu contraseña",
            document.RootElement.GetProperty("mensaje").GetString());
        Assert.Empty(_factory.PasswordResetEmailSender.Messages);
        Assert.Empty(_factory.PasswordResets.All);
    }

    [Fact]
    public async Task Solicitar_ConCorreoMalFormado_DebeResponder400ConErrorDeCampo()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/recuperacion/solicitud",
            new { correo = "no-es-un-correo" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement
            .GetProperty("errors")
            .TryGetProperty("correo", out _));
    }

    [Fact]
    public async Task Confirmar_ConTokenValido_DebeCambiarContrasenaYRevocarSesiones()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var oldTokens = await AuthTestHelpers.ReadTokensAsync(login);

        using var request = await RequestResetAsync(email);
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var rawToken = _factory.PasswordResetEmailSender.Messages[^1].Token;

        using var confirm = await _client.PostAsJsonAsync(
            "/api/v1/auth/recuperacion/confirmacion",
            new { token = rawToken, nuevaContrasena = "NuevaClave1" });

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        using var oldLogin = await AuthTestHelpers.LoginAsync(_client, email);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        using var newLogin = await AuthTestHelpers.LoginAsync(_client, email, "NuevaClave1");
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        using var oldRefresh = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = oldTokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);

        var account = await _factory.Accounts.FindByEmailAsync(EmailAddress.Create(email));
        Assert.True(_factory.Services
            .GetRequiredService<IPasswordHasher>()
            .VerifyPassword(account!.PasswordHash, "NuevaClave1"));
        Assert.True(_factory.PasswordResets.All.Single().IsConsumed);
    }

    [Fact]
    public async Task Confirmar_ConTokenIncorrecto_DebeResponder400YNoCambiarLaContrasena()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var request = await RequestResetAsync(email);
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);

        var validToken = _factory.PasswordResetEmailSender.Messages[^1].Token;
        var wrongToken = validToken[..validToken.IndexOf('.')] +
                         ".BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/recuperacion/confirmacion",
            new { token = wrongToken, nuevaContrasena = "NuevaClave1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var account = await _factory.Accounts.FindByEmailAsync(EmailAddress.Create(email));
        Assert.False(_factory.Services
            .GetRequiredService<IPasswordHasher>()
            .VerifyPassword(account!.PasswordHash, "NuevaClave1"));
        Assert.False(_factory.PasswordResets.All.Single().IsConsumed);
        Assert.Equal(1, _factory.PasswordResets.All.Single().FailedAttempts);
    }

    [Fact]
    public async Task Confirmar_ConTokenVencido_DebeResponder410()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);
        var generator = _factory.Services.GetRequiredService<IPasswordResetTokenGenerator>();
        var generated = generator.Generate();
        var expired = PasswordResetToken.Issue(
            generated.Id,
            account.Id,
            generated.Hash,
            DateTimeOffset.UtcNow.AddMinutes(-31));
        await _factory.PasswordResets.AddAsync(expired);

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/recuperacion/confirmacion",
            new { token = generated.Value, nuevaContrasena = "NuevaClave1" });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Confirmar_ReutilizandoElMismoToken_DebeResponder400()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var request = await RequestResetAsync(email);
        var rawToken = _factory.PasswordResetEmailSender.Messages[^1].Token;

        using var first = await _client.PostAsJsonAsync(
            "/api/v1/auth/recuperacion/confirmacion",
            new { token = rawToken, nuevaContrasena = "NuevaClave1" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await _client.PostAsJsonAsync(
            "/api/v1/auth/recuperacion/confirmacion",
            new { token = rawToken, nuevaContrasena = "OtraClave2" });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        using var oldPassword = await AuthTestHelpers.LoginAsync(_client, email);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        using var newPassword = await AuthTestHelpers.LoginAsync(_client, email, "NuevaClave1");
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("token-invalido")]
    [InlineData("00112233445566778899aabbccddeeff.corto")]
    public async Task Confirmar_ConTokenInvalido_DebeResponder400(string? token)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/recuperacion/confirmacion",
            new { token, nuevaContrasena = "NuevaClave1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corta")]
    public async Task Confirmar_ConContrasenaInvalida_DebeResponder400ConErrorDeCampo(
        string? newPassword)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/recuperacion/confirmacion",
            new
            {
                token = AuthTestHelpers.InvalidPasswordResetToken(),
                nuevaContrasena = newPassword
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement
            .GetProperty("errors")
            .TryGetProperty("nuevaContrasena", out _));
    }

    [Fact]
    public async Task Recuperacion_ConCuerpoMayorAlLimite_DebeResponder413()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/recuperacion/solicitud",
            new { correo = new string('a', 16 * 1024) });

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private Task<HttpResponseMessage> RequestResetAsync(string email) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/recuperacion/solicitud",
            new { correo = email });
}
