using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Domain.Accounts;

namespace UniHome.IntegrationTests;

public sealed class RegisterEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private const string Password = "ClaveSegura1";

    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public RegisterEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Registro_ConDatosValidos_DebeResponder201SinRolNiSecretos()
    {
        var email = UniqueEmail();

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/registro",
            ValidPayload(email));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(email, root.GetProperty("correo").GetString());
        Assert.False(root.GetProperty("correoConfirmado").GetBoolean());
        Assert.True(root.GetProperty("requiereConfirmacionCorreo").GetBoolean());
        Assert.True(root.GetProperty("correoConfirmacionEnviado").GetBoolean());
        Assert.NotEqual(Guid.Empty, root.GetProperty("id").GetGuid());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        Assert.DoesNotContain("rol", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, body, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);

        var account = await _factory.Accounts.FindByEmailAsync(EmailAddress.Create(email));
        Assert.NotNull(account);
        Assert.Equal("Ana López", account.FullName);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.False(account.IsEmailConfirmed);
        Assert.Equal("2026-01", account.PrivacyAcceptance.Version);
        Assert.NotEqual(Password, account.PasswordHash);

        Assert.NotNull(account.EmailVerification);

        var message = Assert.Single(
            _factory.EmailSender.Messages,
            candidate => candidate.Email == email);
        Assert.Matches("^[0-9]{6}$", message.Code);
        Assert.NotEqual(message.Code, account.EmailVerification.CodeHash);
        Assert.DoesNotContain(message.Code, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registro_ConCorreoDuplicado_DebeResponder409SinDuplicarLaCuenta()
    {
        var email = UniqueEmail();

        using var first = await _client.PostAsJsonAsync(
            "/api/v1/auth/registro",
            ValidPayload(email));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var accountsBefore = _factory.Accounts.Count;
        var messagesBefore = _factory.EmailSender.Messages.Count;

        using var second = await _client.PostAsJsonAsync(
            "/api/v1/auth/registro",
            ValidPayload($"  {email.ToUpperInvariant()}  "));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);

        var body = await second.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(409, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correo ya registrado", body, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(accountsBefore, _factory.Accounts.Count);
        Assert.Equal(messagesBefore, _factory.EmailSender.Messages.Count);
    }

    [Fact]
    public async Task Registro_ConConfirmacionDistinta_DebeResponder400ConErrorDelCampo()
    {
        var payload = ValidPayload(UniqueEmail());
        payload["confirmarContrasena"] = "OtraClave1";

        using var response = await _client.PostAsJsonAsync("/api/v1/auth/registro", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty("confirmarContrasena", out var messages));
        Assert.NotEmpty(messages.EnumerateArray());
    }

    [Fact]
    public async Task Registro_SinAceptarPrivacidad_DebeResponder400ConErrorDelCampo()
    {
        var payload = ValidPayload(UniqueEmail());
        payload["aceptoAvisoPrivacidad"] = false;

        using var response = await _client.PostAsJsonAsync("/api/v1/auth/registro", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty("aceptoAvisoPrivacidad", out _));
    }

    [Fact]
    public async Task Registro_ConFechaFutura_DebeResponder400ConErrorDelCampo()
    {
        var payload = ValidPayload(UniqueEmail());
        payload["fechaNacimiento"] = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd");

        using var response = await _client.PostAsJsonAsync("/api/v1/auth/registro", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty("fechaNacimiento", out _));
    }

    [Fact]
    public async Task Registro_ConCorreoInvalido_DebeResponder400ConErrorDelCampo()
    {
        var payload = ValidPayload("no-es-un-correo");

        using var response = await _client.PostAsJsonAsync("/api/v1/auth/registro", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty("correo", out _));
    }

    [Fact]
    public async Task Registro_ConCuerpoMayorAlLimite_DebeResponder413()
    {
        var payload = ValidPayload(UniqueEmail());
        payload["versionAvisoPrivacidad"] = new string('v', 16 * 1024);

        using var response = await _client.PostAsJsonAsync("/api/v1/auth/registro", payload);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.RequestEntityTooLarge, body);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(413, document.RootElement.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("Otro")]
    [InlineData("0")]
    public async Task Registro_ConGeneroFueraDelContrato_DebeResponder400(string genero)
    {
        var email = UniqueEmail();
        var payload = ValidPayload(email);
        payload["genero"] = genero == "0" ? 0 : genero;

        using var response = await _client.PostAsJsonAsync("/api/v1/auth/registro", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotEmpty(document.RootElement.GetProperty("errors").EnumerateObject());

        Assert.Null(await _factory.Accounts.FindByEmailAsync(EmailAddress.Create(email)));
    }

    private static Dictionary<string, object?> ValidPayload(string email) =>
        new()
        {
            ["nombreCompleto"] = "Ana López",
            ["correo"] = email,
            ["fechaNacimiento"] = "2004-05-10",
            ["genero"] = "Mujer",
            ["contrasena"] = Password,
            ["confirmarContrasena"] = Password,
            ["aceptoAvisoPrivacidad"] = true,
            ["versionAvisoPrivacidad"] = "2026-01"
        };

    private static string UniqueEmail() => $"ana.{Guid.NewGuid():N}@uni.edu";
}
