using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Identity;

namespace UniHome.IntegrationTests;

public sealed class ConfirmEmailEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public ConfirmEmailEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Confirmar_ConCodigoCorrecto_DebeResponder200YConfirmarLaCuenta()
    {
        var email = await RegisterAsync();
        var code = GetLastCode(email);

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo",
            new { correo = email, codigo = code });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("correoConfirmado").GetBoolean());

        var account = await _factory.Accounts.FindByEmailAsync(EmailAddress.Create(email));
        Assert.True(account!.IsEmailConfirmed);
    }

    [Fact]
    public async Task Confirmar_Repetido_DebeResponder200DeFormaIdempotente()
    {
        var email = await RegisterAsync();
        var code = GetLastCode(email);

        using var first = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo",
            new { correo = email, codigo = code });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo",
            new { correo = email, codigo = code });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var document = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("correoConfirmado").GetBoolean());
    }

    [Fact]
    public async Task Confirmar_ConCodigoIncorrecto_DebeResponder400SinRevelarNada()
    {
        var email = await RegisterAsync();

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo",
            new { correo = email, codigo = "000000" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);

        var account = await _factory.Accounts.FindByEmailAsync(EmailAddress.Create(email));
        Assert.False(account!.IsEmailConfirmed);
    }

    [Fact]
    public async Task Confirmar_ConCorreoInexistente_DebeResponder400IgualQueCodigoIncorrecto()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo",
            new { correo = AuthTestHelpers.UniqueEmail(), codigo = "123456" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Confirmar_ConCodigoVencido_DebeResponder410()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email, emailConfirmed: false);
        account.IssueEmailVerification(Protect("123456"), DateTimeOffset.UtcNow.Add(-VerificationCode.Lifetime).AddMinutes(-1));
        await _factory.Accounts.UpdateAsync(account);

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo",
            new { correo = email, codigo = "123456" });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Confirmar_TrasAgotarIntentosPorCuenta_DebeResponder410()
    {
        var email = await RegisterAsync();

        for (var attempt = 0; attempt < VerificationCode.MaxConfirmationAttempts; attempt++)
        {
            using var incorrect = await _client.PostAsJsonAsync(
                "/api/v1/auth/confirmacion-correo",
                new { correo = email, codigo = "000000" });
            Assert.Equal(HttpStatusCode.BadRequest, incorrect.StatusCode);
        }

        var code = GetLastCode(email);
        using var afterLimit = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo",
            new { correo = email, codigo = code });

        Assert.Equal(HttpStatusCode.Gone, afterLimit.StatusCode);
    }

    [Theory]
    [InlineData(null, "123456")]
    [InlineData("correo-invalido", "123456")]
    [InlineData("ana@uni.edu", "12a456")]
    [InlineData("ana@uni.edu", null)]
    public async Task Confirmar_ConDatosInvalidos_DebeResponder400ConErrorDeCampo(string? correo, string? codigo)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo",
            new { correo, codigo });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotEmpty(document.RootElement.GetProperty("errors").EnumerateObject());
    }

    [Fact]
    public async Task Confirmar_ConCuerpoMayorAlLimite_DebeResponder413()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo",
            new { correo = new string('a', 16 * 1024), codigo = "123456" });

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private async Task<string> RegisterAsync()
    {
        var email = AuthTestHelpers.UniqueEmail();

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/registro",
            new
            {
                nombreCompleto = "Ana López",
                correo = email,
                fechaNacimiento = "2004-05-10",
                genero = "Mujer",
                contrasena = AuthTestHelpers.Password,
                confirmarContrasena = AuthTestHelpers.Password,
                aceptoAvisoPrivacidad = true,
                versionAvisoPrivacidad = "2026-01"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return email;
    }

    private string GetLastCode(string email) =>
        _factory.EmailSender.Messages.Last(message => message.Email == email).Code;

    private static string Protect(string code) => TestCodeProtector.Protect(code);

    private static readonly HmacVerificationCodeProtector TestCodeProtector = new(
        Options.Create(new VerificationCodeOptions
        {
            HmacSecret = UniHomeApiFactory.TestVerificationCodeHmacSecret
        }),
        NullLogger<HmacVerificationCodeProtector>.Instance);
}
