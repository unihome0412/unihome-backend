namespace UniHome.Application.Identity.Login;

public enum LoginStatus
{
    Success,
    InvalidCredentials,
    EmailNotConfirmed,
    Suspended,
    Deleted,
    ValidationFailed
}

/// <summary>
/// Resultado del login. Las credenciales inválidas y las cuentas eliminadas se
/// representan igual para no revelar la existencia de una cuenta.
/// </summary>
public sealed class LoginResult
{
    private LoginResult(
        LoginStatus status,
        IReadOnlyDictionary<string, string[]> validationErrors,
        string? accessToken,
        string? refreshToken,
        DateTimeOffset? expiresAt)
    {
        Status = status;
        ValidationErrors = validationErrors;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        ExpiresAt = expiresAt;
    }

    public LoginStatus Status { get; }

    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    public string? AccessToken { get; }

    public string? RefreshToken { get; }

    public DateTimeOffset? ExpiresAt { get; }

    public bool Succeeded => Status == LoginStatus.Success;

    public static LoginResult InvalidCredentials() =>
        new(LoginStatus.InvalidCredentials, EmptyErrors, null, null, null);

    public static LoginResult EmailNotConfirmed() =>
        new(LoginStatus.EmailNotConfirmed, EmptyErrors, null, null, null);

    public static LoginResult Suspended() =>
        new(LoginStatus.Suspended, EmptyErrors, null, null, null);

    public static LoginResult Deleted() =>
        new(LoginStatus.Deleted, EmptyErrors, null, null, null);

    public static LoginResult ValidationFailed(IReadOnlyDictionary<string, string[]> errors) =>
        new(LoginStatus.ValidationFailed, errors, null, null, null);

    public static LoginResult Success(
        string accessToken,
        string refreshToken,
        DateTimeOffset expiresAt) =>
        new(LoginStatus.Success, EmptyErrors, accessToken, refreshToken, expiresAt);

    private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);
}
