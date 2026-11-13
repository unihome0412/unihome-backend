using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using UniHome.Api.Contracts.Auth;
using UniHome.Domain.Accounts;

namespace UniHome.IntegrationTests;

internal static class AuthTestHelpers
{
    public const string Password = "ClaveSegura1";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public static string UniqueEmail() => $"ana.{Guid.NewGuid():N}@uni.edu";

    public static async Task<Account> SeedAccountAsync(
        UniHomeApiFactory factory,
        string email,
        AccountStatus status = AccountStatus.Active,
        bool emailConfirmed = true,
        Guid? id = null)
    {
        var account = TestSupport.TestAccountFactory.Create(
            email,
            Password,
            status,
            emailConfirmed,
            id);

        await factory.Accounts.AddAsync(account);
        return account;
    }

    public static async Task<Account> SeedUnconfirmedAccountWithVerificationAsync(
        UniHomeApiFactory factory,
        string email)
    {
        var account = TestSupport.TestAccountFactory.Create(
            email,
            Password,
            AccountStatus.Active,
            emailConfirmed: false);

        account.IssueEmailVerification(
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"protected:{email}")),
            DateTimeOffset.UtcNow);

        await factory.Accounts.AddAsync(account);
        return account;
    }

    public static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password = Password) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { correo = email, contrasena = password });

    public static async Task<LoginResponse> ReadTokensAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<LoginResponse>(json, SerializerOptions)
            ?? throw new InvalidOperationException("La respuesta no contiene tokens.");
    }

    public static Guid GetSessionId(string refreshToken)
    {
        var separator = refreshToken.IndexOf('.', StringComparison.Ordinal);

        if (separator <= 0)
        {
            throw new InvalidOperationException("El refresh token no contiene la sesión.");
        }

        return Guid.ParseExact(refreshToken[..separator], "N");
    }

    public static string InvalidPasswordResetToken() =>
        $"{Guid.NewGuid():N}.{new string('B', 43)}";
}
